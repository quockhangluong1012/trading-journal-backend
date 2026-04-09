using Microsoft.Extensions.Logging;

namespace TradingJournal.Modules.Backtest.Services;

/// <summary>
/// Core matching engine that evaluates each OHLCV candle against pending limit orders
/// and active positions. Simulates real-world broker execution mechanics.
///
/// OHLC DATA CONVENTION: All incoming candle data represents BID prices.
/// ASK prices are derived as: Ask = Bid + spread.
///
/// This engine implements three critical execution features:
///
/// 1. BID/ASK SIMULATION ("Phantom Spread")
///    - Long positions open at ASK, close (SL/TP) at BID.
///    - Short positions open at BID, close (SL/TP) at ASK.
///
/// 2. PRICE GAP & SLIPPAGE
///    - Before checking High/Low wicks, the Open price is checked against SL/TP.
///    - If the Open has gapped past the requested level, execution occurs at the
///      Open price (Bid/Ask adjusted), not the requested SL/TP.
///
/// 3. INTRA-BAR COLLISION ("Schrödinger's Candle")
///    - When both SL and TP fall within a single bar's range:
///      Bullish candle (Close >= Open): assume path Open → Low → High → Close
///      Bearish candle (Open > Close):  assume path Open → High → Low → Close
///      The first extreme in the assumed path determines which exit triggers.
///
/// PnL calculation:
///    Long:  (exitPrice - entryPrice) * positionSize
///    Short: (entryPrice - exitPrice) * positionSize
/// </summary>
internal sealed class OrderMatchingEngine(ILogger<OrderMatchingEngine> logger) : IOrderMatchingEngine
{
    public MatchingResult EvaluateCandle(
        OhlcvCandle candle,
        List<BacktestOrder> pendingOrders,
        List<BacktestOrder> activePositions,
        decimal currentBalance,
        decimal spread)
    {
        List<OrderFill> fills = [];
        List<OrderClose> closes = [];
        decimal balance = currentBalance;

        // ── Pre-compute ASK-side OHLC ──────────────────────
        // Candle data = BID. ASK = BID + spread.
        decimal askOpen = candle.Open + spread;
        decimal askHigh = candle.High + spread;
        decimal askLow = candle.Low + spread;

        // ─────────────────────────────────────────────────
        // 1. Evaluate Pending Limit Orders for fills
        // ─────────────────────────────────────────────────
        foreach (BacktestOrder order in pendingOrders)
        {
            OrderFill? fill = TryFillLimitOrder(candle, order, spread);

            if (fill is not null)
            {
                fills.Add(fill);

                // Move this order to the active pool so it's evaluated for SL/TP
                // in the same candle (important for volatile candles)
                order.Status = BacktestOrderStatus.Active;
                order.FilledPrice = fill.FilledPrice;
                order.FilledAt = fill.FilledAt;
                activePositions.Add(order);

                logger.LogDebug(
                    "Limit order {OrderId} ({Side}) filled at {Price} on candle {Timestamp}",
                    order.Id, order.Side, fill.FilledPrice, candle.Timestamp);
            }
        }

        // ─────────────────────────────────────────────────
        // 2. Evaluate Active Positions for SL/TP hits
        //    Execution order: Gap Check → Intra-bar Collision
        // ─────────────────────────────────────────────────
        List<BacktestOrder> remainingPositions = [];

        foreach (BacktestOrder position in activePositions)
        {
            OrderClose? closeResult = EvaluatePosition(candle, position, spread, askOpen, askHigh, askLow);

            if (closeResult is not null)
            {
                closes.Add(closeResult);
                balance += closeResult.Pnl;

                logger.LogDebug(
                    "Position {OrderId} ({Side}) closed: {Reason} at {ExitPrice}, PnL: {Pnl}, Slippage: {Slippage}",
                    position.Id, position.Side, closeResult.Reason,
                    closeResult.ExitPrice, closeResult.Pnl, closeResult.Slippage);
            }
            else
            {
                remainingPositions.Add(position);
            }
        }

        // ─────────────────────────────────────────────────
        // 3. Calculate Unrealized PnL on remaining positions
        //    Use Bid Close for Longs, Ask Close for Shorts
        // ─────────────────────────────────────────────────
        decimal unrealizedPnl = 0m;
        decimal askClose = candle.Close + spread;

        foreach (BacktestOrder position in remainingPositions)
        {
            decimal markPrice = position.Side == BacktestOrderSide.Long
                ? candle.Close   // Longs close at Bid
                : askClose;      // Shorts close at Ask

            unrealizedPnl += CalculateUnrealizedPnl(position, markPrice);
        }

        decimal equity = balance + unrealizedPnl;

        // ─────────────────────────────────────────────────
        // 4. Liquidation check
        // ─────────────────────────────────────────────────
        bool isLiquidated = equity <= 0m;

        if (isLiquidated)
        {
            logger.LogWarning("LIQUIDATION triggered. Equity: {Equity}, Balance: {Balance}", equity, balance);

            // Force close all remaining positions at current candle close (Bid/Ask adjusted)
            foreach (BacktestOrder position in remainingPositions)
            {
                decimal exitPrice = position.Side == BacktestOrderSide.Long
                    ? candle.Close   // Longs close at Bid
                    : askClose;      // Shorts close at Ask

                decimal pnl = CalculateRealizedPnl(position, exitPrice);
                closes.Add(new OrderClose(position.Id, exitPrice, pnl, "Liquidated", candle.Timestamp));
                balance += pnl;
            }

            equity = balance;
        }

        return new MatchingResult(fills, closes, unrealizedPnl, equity, isLiquidated);
    }

    // ═══════════════════════════════════════════════════════
    //  LIMIT ORDER EVALUATION
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Evaluates whether a pending limit order should fill on this candle.
    ///
    /// BID/ASK rules for limit orders:
    /// - Buy Limit (Long): The trader wants to buy at a specific ASK price or lower.
    ///   Since Ask = Bid + spread, the order triggers when Ask Low (= candle.Low + spread)
    ///   drops to or below the entry price. Fill price = entry price (the ASK price requested).
    ///   If the Ask has gapped below the entry price at Open, fill at askOpen (beneficial gap).
    ///
    /// - Sell Limit (Short): The trader wants to sell at a specific BID price or higher.
    ///   Triggers when Bid High (= candle.High) rises to or above the entry price.
    ///   Fill price = entry price (the BID price requested).
    ///   If Bid has gapped above the entry price at Open, fill at bidOpen (beneficial gap).
    /// </summary>
    private static OrderFill? TryFillLimitOrder(OhlcvCandle candle, BacktestOrder order, decimal spread)
    {
        if (order.OrderType != BacktestOrderType.Limit || order.Status != BacktestOrderStatus.Pending)
            return null;

        decimal entryPrice = order.EntryPrice;

        switch (order.Side)
        {
            case BacktestOrderSide.Long:
            {
                decimal askLow = candle.Low + spread;
                decimal askOpen = candle.Open + spread;

                // Check if Ask price range reaches the entry level
                if (askLow > entryPrice)
                    return null; // Ask never dipped low enough

                // Gap check: if Ask opened below entry, fill at the (better) open price
                decimal filledPrice = askOpen <= entryPrice ? askOpen : entryPrice;

                return new OrderFill(order.Id, filledPrice, candle.Timestamp);
            }

            case BacktestOrderSide.Short:
            {
                decimal bidHigh = candle.High;
                decimal bidOpen = candle.Open;

                // Check if Bid price range reaches the entry level
                if (bidHigh < entryPrice)
                    return null; // Bid never rose high enough

                // Gap check: if Bid opened above entry, fill at the (better) open price
                decimal filledPrice = bidOpen >= entryPrice ? bidOpen : entryPrice;

                return new OrderFill(order.Id, filledPrice, candle.Timestamp);
            }

            default:
                return null;
        }
    }

    // ═══════════════════════════════════════════════════════
    //  POSITION EVALUATION (SL/TP)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Evaluates an active position against the current candle for SL/TP hits.
    ///
    /// Execution order (per the three critical features):
    ///   Step 1 — GAP CHECK: Compare the bar's Open (Bid/Ask adjusted) against SL/TP.
    ///            If the open has gapped past the requested level, execute at the Open
    ///            price with slippage (no false fill at the requested level).
    ///   Step 2 — INTRA-BAR COLLISION: If both SL and TP lie within the candle's range,
    ///            use candle shape to determine which extreme was hit first:
    ///            Bullish (Close >= Open): path = Open → Low → High → Close
    ///            Bearish (Open > Close):  path = Open → High → Low → Close
    /// </summary>
    private static OrderClose? EvaluatePosition(
        OhlcvCandle candle,
        BacktestOrder position,
        decimal spread,
        decimal askOpen,
        decimal askHigh,
        decimal askLow)
    {
        decimal filledPrice = position.FilledPrice ?? position.EntryPrice;
        decimal? sl = position.StopLoss;
        decimal? tp = position.TakeProfit;

        // ── STEP 1: GAP CHECK on Open ──────────────────
        // Determine the effective open price for this position's close side
        // Longs close at BID → use candle.Open (Bid Open)
        // Shorts close at ASK → use askOpen
        decimal effectiveOpen = position.Side == BacktestOrderSide.Long
            ? candle.Open   // Bid Open
            : askOpen;      // Ask Open

        // Check SL gap first (worst-case priority at the gap level)
        if (sl.HasValue)
        {
            bool slGapped = position.Side switch
            {
                // Long SL: price must drop below SL → gap if Bid Open < SL
                BacktestOrderSide.Long => candle.Open <= sl.Value,
                // Short SL: price must rise above SL → gap if Ask Open >= SL
                BacktestOrderSide.Short => askOpen >= sl.Value,
                _ => false
            };

            if (slGapped)
            {
                decimal intendedPrice = sl.Value;
                decimal slippage = effectiveOpen - intendedPrice;
                decimal pnl = CalculateRealizedPnlStatic(position.Side, filledPrice, effectiveOpen, position.PositionSize);

                return new OrderClose(position.Id, effectiveOpen, pnl, "SL Hit (Gapped)", candle.Timestamp, slippage);
            }
        }

        // Check TP gap (beneficial gap)
        if (tp.HasValue)
        {
            bool tpGapped = position.Side switch
            {
                // Long TP: price must rise above TP → gap if Bid Open > TP
                BacktestOrderSide.Long => candle.Open >= tp.Value,
                // Short TP: price must drop below TP → gap if Bid Open < TP
                // Short TP: price must drop below TP → gap if Ask Open <= TP
                BacktestOrderSide.Short => askOpen <= tp.Value,
                _ => false
            };

            if (tpGapped)
            {
                decimal intendedPrice = tp.Value;
                decimal slippage = effectiveOpen - intendedPrice;
                decimal pnl = CalculateRealizedPnlStatic(position.Side, filledPrice, effectiveOpen, position.PositionSize);

                return new OrderClose(position.Id, effectiveOpen, pnl, "TP Hit (Gapped)", candle.Timestamp, slippage);
            }
        }

        // ── STEP 2: INTRA-BAR COLLISION (Wick evaluation) ──
        // No gap triggered. Now check if SL/TP are hit within the candle's High/Low range.
        // Use candle shape heuristic to determine evaluation order.

        bool isBullish = candle.Close >= candle.Open;

        // Determine which prices to use for SL/TP comparison based on position side:
        // Long closes at BID:  compare SL/TP against Bid Low / Bid High
        // Short closes at ASK: compare SL/TP against Ask High / Ask Low

        if (isBullish)
        {
            // Bullish candle: assumed path Open → Low → High → Close
            // Evaluate Low extreme FIRST, then High extreme

            // Low extreme
            OrderClose? lowResult = EvaluateExtreme(position, filledPrice, candle, spread, askHigh, askLow, isLowExtreme: true);
            if (lowResult is not null) return lowResult;

            // High extreme
            OrderClose? highResult = EvaluateExtreme(position, filledPrice, candle, spread, askHigh, askLow, isLowExtreme: false);
            if (highResult is not null) return highResult;
        }
        else
        {
            // Bearish candle: assumed path Open → High → Low → Close
            // Evaluate High extreme FIRST, then Low extreme

            // High extreme
            OrderClose? highResult = EvaluateExtreme(position, filledPrice, candle, spread, askHigh, askLow, isLowExtreme: false);
            if (highResult is not null) return highResult;

            // Low extreme
            OrderClose? lowResult = EvaluateExtreme(position, filledPrice, candle, spread, askHigh, askLow, isLowExtreme: true);
            if (lowResult is not null) return lowResult;
        }

        return null;
    }

    /// <summary>
    /// Evaluates a single price extreme (Low or High) of the candle against a position's SL/TP.
    ///
    /// For the LOW extreme:
    ///   - Long: SL check (Bid Low ≤ SL?)
    ///   - Short: TP check (Ask Low ≤ TP?)
    ///
    /// For the HIGH extreme:
    ///   - Long: TP check (Bid High ≥ TP?)
    ///   - Short: SL check (Ask High ≥ SL?)
    /// </summary>
    private static OrderClose? EvaluateExtreme(
        BacktestOrder position,
        decimal filledPrice,
        OhlcvCandle candle,
        decimal spread,
        decimal askHigh,
        decimal askLow,
        bool isLowExtreme)
    {
        decimal? sl = position.StopLoss;
        decimal? tp = position.TakeProfit;

        if (isLowExtreme)
        {
            // ── LOW EXTREME ──
            switch (position.Side)
            {
                case BacktestOrderSide.Long:
                    // Long SL: Does Bid Low reach the SL level?
                    if (sl.HasValue && candle.Low <= sl.Value)
                    {
                        decimal exitPrice = sl.Value;
                        decimal pnl = CalculateRealizedPnlStatic(position.Side, filledPrice, exitPrice, position.PositionSize);
                        return new OrderClose(position.Id, exitPrice, pnl, "SL Hit", candle.Timestamp);
                    }
                    break;

                case BacktestOrderSide.Short:
                    // Short TP: Does Ask Low reach the TP level?
                    if (tp.HasValue && askLow <= tp.Value)
                    {
                        decimal exitPrice = tp.Value;
                        decimal pnl = CalculateRealizedPnlStatic(position.Side, filledPrice, exitPrice, position.PositionSize);
                        return new OrderClose(position.Id, exitPrice, pnl, "TP Hit", candle.Timestamp);
                    }
                    break;
            }
        }
        else
        {
            // ── HIGH EXTREME ──
            switch (position.Side)
            {
                case BacktestOrderSide.Long:
                    // Long TP: Does Bid High reach the TP level?
                    if (tp.HasValue && candle.High >= tp.Value)
                    {
                        decimal exitPrice = tp.Value;
                        decimal pnl = CalculateRealizedPnlStatic(position.Side, filledPrice, exitPrice, position.PositionSize);
                        return new OrderClose(position.Id, exitPrice, pnl, "TP Hit", candle.Timestamp);
                    }
                    break;

                case BacktestOrderSide.Short:
                    // Short SL: Does Ask High reach the SL level?
                    if (sl.HasValue && askHigh >= sl.Value)
                    {
                        decimal exitPrice = sl.Value;
                        decimal pnl = CalculateRealizedPnlStatic(position.Side, filledPrice, exitPrice, position.PositionSize);
                        return new OrderClose(position.Id, exitPrice, pnl, "SL Hit", candle.Timestamp);
                    }
                    break;
            }
        }

        return null;
    }

    // ═══════════════════════════════════════════════════════
    //  PnL CALCULATIONS
    // ═══════════════════════════════════════════════════════

    private static decimal CalculateUnrealizedPnl(BacktestOrder position, decimal currentPrice)
    {
        decimal filledPrice = position.FilledPrice ?? position.EntryPrice;
        return CalculateRealizedPnlStatic(position.Side, filledPrice, currentPrice, position.PositionSize);
    }

    private static decimal CalculateRealizedPnl(BacktestOrder position, decimal exitPrice)
    {
        decimal filledPrice = position.FilledPrice ?? position.EntryPrice;
        return CalculateRealizedPnlStatic(position.Side, filledPrice, exitPrice, position.PositionSize);
    }

    /// <summary>
    /// PnL calculation:
    /// - Long: (exitPrice - entryPrice) * positionSize
    /// - Short: (entryPrice - exitPrice) * positionSize
    /// </summary>
    private static decimal CalculateRealizedPnlStatic(
        BacktestOrderSide side, decimal entryPrice, decimal exitPrice, decimal positionSize)
    {
        return side switch
        {
            BacktestOrderSide.Long => (exitPrice - entryPrice) * positionSize,
            BacktestOrderSide.Short => (entryPrice - exitPrice) * positionSize,
            _ => 0m
        };
    }
}
