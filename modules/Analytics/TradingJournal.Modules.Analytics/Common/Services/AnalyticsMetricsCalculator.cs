using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Analytics.Common.Services;

/// <summary>
/// Centralized analytics metric calculator that eliminates duplicated computation
/// across GetPerformanceSummary and GetInsights handlers.
/// </summary>
public sealed class AnalyticsMetricsCalculator
{
    /// <summary>
    /// Computed analytics metrics from a set of closed trades.
    /// </summary>
    public sealed record AnalyticsMetrics
    {
        public decimal TotalPnl { get; init; }
        public decimal WinRate { get; init; }
        public int WinCount { get; init; }
        public int LossCount { get; init; }
        public int TotalClosed { get; init; }
        public decimal AvgWin { get; init; }
        public decimal AvgLoss { get; init; }
        public decimal LargestWin { get; init; }
        public decimal LargestLoss { get; init; }
        public decimal ProfitFactor { get; init; }
        public decimal Expectancy { get; init; }
        public decimal MaxDrawdown { get; init; }
        public decimal MaxDrawdownPct { get; init; }
        public decimal SharpeRatio { get; init; }
        public decimal AvgHoldingDays { get; init; }
        public decimal LongsWinRate { get; init; }
        public decimal ShortsWinRate { get; init; }
        public int ConsecutiveWins { get; init; }
        public int ConsecutiveLosses { get; init; }
        public decimal AvgRiskReward { get; init; }

        // Asset-level breakdowns (used by insights)
        public AssetPnl? BestAsset { get; init; }
        public AssetPnl? WorstAsset { get; init; }
    }

    public sealed record AssetPnl(string Asset, decimal Pnl);

    /// <summary>
    /// Filters closed trades by the given date filter and computes all metrics.
    /// Returns null if no closed trades exist in the period.
    /// </summary>
    public static AnalyticsMetrics? Calculate(List<TradeCacheDto> trades, AnalyticsFilter filter)
    {
        DateTime fromDate = AnalyticsFilterHelper.GetFromDate(filter);

        List<TradeCacheDto> closed = [.. trades
            .Where(t => t.Status == TradeStatus.Closed && t.Pnl.HasValue)
            .Where(t => fromDate == DateTime.MinValue || (t.ClosedDate.HasValue && t.ClosedDate.Value >= fromDate))];

        if (closed.Count == 0)
            return null;

        return CalculateFromClosed(closed);
    }

    /// <summary>
    /// Computes all metrics from a pre-filtered list of closed trades.
    /// </summary>
    public static AnalyticsMetrics CalculateFromClosed(List<TradeCacheDto> closed)
    {
        List<TradeCacheDto> wins = closed.Where(t => t.Pnl > 0).ToList();
        List<TradeCacheDto> losses = closed.Where(t => t.Pnl <= 0).ToList();

        decimal totalPnl = closed.Sum(t => (decimal)t.Pnl!.Value);
        decimal winRate = (decimal)wins.Count / closed.Count * 100;
        decimal avgWin = wins.Count > 0 ? wins.Average(t => (decimal)t.Pnl!.Value) : 0;
        decimal avgLoss = losses.Count > 0 ? Math.Abs(losses.Average(t => (decimal)t.Pnl!.Value)) : 0;
        decimal largestWin = wins.Count > 0 ? (decimal)wins.Max(t => t.Pnl!.Value) : 0;
        decimal largestLoss = losses.Count > 0 ? (decimal)losses.Min(t => t.Pnl!.Value) : 0;

        // Profit factor
        decimal grossProfit = wins.Sum(t => (decimal)t.Pnl!.Value);
        decimal grossLoss = Math.Abs(losses.Sum(t => (decimal)t.Pnl!.Value));
        decimal profitFactor = grossLoss > 0 ? grossProfit / grossLoss : (grossProfit > 0 ? decimal.MaxValue : 0);

        // Expectancy
        decimal expectancy = (winRate / 100 * avgWin) - ((1 - winRate / 100) * avgLoss);

        // Max drawdown
        List<TradeCacheDto> sorted = closed
            .Where(t => t.ClosedDate.HasValue)
            .OrderBy(t => t.ClosedDate!.Value)
            .ToList();

        decimal peak = 0, equity = 0, maxDD = 0, maxDDPct = 0;
        foreach (TradeCacheDto t in sorted)
        {
            equity += (decimal)t.Pnl!.Value;
            if (equity > peak) peak = equity;
            decimal dd = peak - equity;
            if (dd > maxDD)
            {
                maxDD = dd;
                maxDDPct = peak > 0 ? dd / peak * 100 : 0;
            }
        }

        // Sharpe ratio (simplified, annualized)
        double[] returns = sorted.Select(t => (double)t.Pnl!.Value).ToArray();
        decimal meanReturn = returns.Length > 0 ? (decimal)returns.Average() : 0;
        decimal stdDev = returns.Length > 1
            ? (decimal)Math.Sqrt(returns.Sum(r => Math.Pow(r - (double)meanReturn, 2)) / (returns.Length - 1))
            : 0;
        decimal sharpeRatio = stdDev > 0 ? meanReturn / stdDev * (decimal)Math.Sqrt(252) : 0;

        // Avg holding days
        double[] holdingDays = closed
            .Where(t => t.ClosedDate.HasValue)
            .Select(t => (t.ClosedDate!.Value - t.Date).TotalDays)
            .ToArray();
        decimal avgHoldingDays = holdingDays.Length > 0 ? (decimal)holdingDays.Average() : 0;

        // Long vs Short win rates
        List<TradeCacheDto> longs = [.. closed.Where(t => t.Position == PositionType.Long)];
        List<TradeCacheDto> shorts = [.. closed.Where(t => t.Position == PositionType.Short)];
        decimal longsWinRate = longs.Count > 0 ? (decimal)longs.Count(t => t.Pnl > 0) / longs.Count * 100 : 0;
        decimal shortsWinRate = shorts.Count > 0 ? (decimal)shorts.Count(t => t.Pnl > 0) / shorts.Count * 100 : 0;

        // Consecutive wins/losses
        int maxConsecWins = 0, maxConsecLosses = 0, curWins = 0, curLosses = 0;
        foreach (TradeCacheDto t in sorted)
        {
            if (t.Pnl > 0) { curWins++; curLosses = 0; maxConsecWins = Math.Max(maxConsecWins, curWins); }
            else { curLosses++; curWins = 0; maxConsecLosses = Math.Max(maxConsecLosses, curLosses); }
        }

        // Avg risk-reward
        double[] rrValues = closed
            .Where(t => t.StopLoss > 0 && t.TargetTier1 > 0 && t.EntryPrice > 0)
            .Select(t =>
            {
                decimal risk = Math.Abs(t.EntryPrice - t.StopLoss);
                decimal reward = Math.Abs(t.TargetTier1 - t.EntryPrice);
                return risk > 0 ? (double)(reward / risk) : 0;
            })
            .Where(r => r > 0)
            .ToArray();
        decimal avgRiskReward = rrValues.Length > 0 ? (decimal)rrValues.Average() : 0;

        // Asset breakdown (for insights)
        var assetPnl = closed.GroupBy(t => t.Asset)
            .Select(g => new AssetPnl(g.Key, g.Sum(t => (decimal)t.Pnl!.Value)))
            .ToList();
        AssetPnl? bestAsset = assetPnl.OrderByDescending(a => a.Pnl).FirstOrDefault();
        AssetPnl? worstAsset = assetPnl.OrderBy(a => a.Pnl).FirstOrDefault();

        return new AnalyticsMetrics
        {
            TotalPnl = Math.Round(totalPnl, 2),
            WinRate = Math.Round(winRate, 1),
            WinCount = wins.Count,
            LossCount = losses.Count,
            TotalClosed = closed.Count,
            AvgWin = Math.Round(avgWin, 2),
            AvgLoss = Math.Round(avgLoss, 2),
            LargestWin = Math.Round(largestWin, 2),
            LargestLoss = Math.Round(largestLoss, 2),
            ProfitFactor = Math.Round(profitFactor, 2),
            Expectancy = Math.Round(expectancy, 2),
            MaxDrawdown = Math.Round(maxDD, 2),
            MaxDrawdownPct = Math.Round(maxDDPct, 1),
            SharpeRatio = Math.Round(sharpeRatio, 2),
            AvgHoldingDays = Math.Round(avgHoldingDays, 1),
            LongsWinRate = Math.Round(longsWinRate, 1),
            ShortsWinRate = Math.Round(shortsWinRate, 1),
            ConsecutiveWins = maxConsecWins,
            ConsecutiveLosses = maxConsecLosses,
            AvgRiskReward = Math.Round(avgRiskReward, 2),
            BestAsset = bestAsset,
            WorstAsset = worstAsset,
        };
    }
}
