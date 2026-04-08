namespace TradingJournal.Modules.Backtest.Dto;

public record SessionListDto(
    int Id,
    string Asset,
    DateTime StartDate,
    DateTime? EndDate,
    decimal InitialBalance,
    decimal CurrentBalance,
    decimal PnlPercent,
    string Status,
    DateTime CurrentTimestamp,
    bool IsDataReady,
    DateTime CreatedDate);

public record SessionDetailDto(
    int Id,
    string Asset,
    DateTime StartDate,
    DateTime? EndDate,
    decimal InitialBalance,
    decimal CurrentBalance,
    decimal PnlPercent,
    string Status,
    DateTime CurrentTimestamp,
    string ActiveTimeframe,
    int PlaybackSpeed,
    bool IsDataReady,
    int TotalOrders,
    int OpenPositions,
    int ClosedTrades,
    DateTime CreatedDate);

public record PlaybackStateDto(
    int SessionId,
    string Asset,
    DateTime CurrentTimestamp,
    string ActiveTimeframe,
    decimal Balance,
    decimal Equity,
    decimal UnrealizedPnl,
    string Status,
    List<OrderDto> PendingOrders,
    List<OrderDto> ActivePositions,
    string DrawingsJson);

public record OrderDto(
    int Id,
    string OrderType,
    string Side,
    string Status,
    decimal EntryPrice,
    decimal? FilledPrice,
    decimal PositionSize,
    decimal? StopLoss,
    decimal? TakeProfit,
    decimal? ExitPrice,
    decimal? Pnl,
    DateTime OrderedAt,
    DateTime? FilledAt,
    DateTime? ClosedAt);

public record TradeResultDto(
    int Id,
    int OrderId,
    string Side,
    decimal EntryPrice,
    decimal ExitPrice,
    decimal PositionSize,
    decimal Pnl,
    decimal BalanceAfter,
    DateTime EntryTime,
    DateTime ExitTime,
    string ExitReason);

public record CandleDto(
    DateTime Timestamp,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume);

public record AnalyticsDto(
    int TotalTrades,
    int TotalWins,
    int TotalLosses,
    decimal WinRate,
    decimal GrossProfit,
    decimal GrossLoss,
    decimal NetPnl,
    decimal MaxDrawdown,
    List<EquityCurvePoint> EquityCurve,
    List<TradeResultDto> TradeLog);

public record EquityCurvePoint(DateTime Timestamp, decimal Balance);

public record AdvanceCandleResponseDto(
    CandleDto? Candle,
    decimal Balance,
    decimal Equity,
    decimal UnrealizedPnl,
    DateTime CurrentTimestamp,
    bool IsSessionEnded,
    bool IsLiquidated,
    List<OrderDto> FilledOrders,
    List<OrderDto> ClosedPositions);
