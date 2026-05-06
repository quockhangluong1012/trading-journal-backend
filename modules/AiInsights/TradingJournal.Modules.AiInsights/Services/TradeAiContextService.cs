using System.Globalization;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Modules.AiInsights.Services;

public interface ITradeAiContextService
{
    Task<TradeAiContextSnapshot> BuildPatternContextAsync(
        int userId,
        DateTime? fromDate,
        DateTime? toDate,
        int maxTrades,
        CancellationToken cancellationToken);

    Task<TradeAiContextSnapshot> BuildRecentClosedTradesContextAsync(
        int userId,
        int maxTrades,
        CancellationToken cancellationToken);
}

public sealed record TradeAiContextSnapshot(int SampleSize, string TradeDigest, string RangeSummary);

internal sealed class TradeAiContextService(
    ITradeProvider tradeProvider,
    IEmotionTagProvider emotionTagProvider,
    IZoneProvider zoneProvider,
    ITechnicalAnalysisTagProvider technicalAnalysisTagProvider) : ITradeAiContextService
{
    private const int MaxTradeDigestSize = 100;

    public async Task<TradeAiContextSnapshot> BuildPatternContextAsync(
        int userId,
        DateTime? fromDate,
        DateTime? toDate,
        int maxTrades,
        CancellationToken cancellationToken)
    {
        int safeMaxTrades = ClampMaxTrades(maxTrades);
        List<TradeCacheDto> trades = await tradeProvider.GetTradesAsync(userId, cancellationToken);

        DateTime? start = fromDate?.Date;
        DateTime? end = toDate?.Date.AddDays(1).AddTicks(-1);

        List<TradeCacheDto> filteredTrades = [.. trades
            .Where(trade => trade.Status == Shared.Common.Enum.TradeStatus.Closed && trade.Pnl.HasValue && trade.ClosedDate.HasValue)
            .Where(trade => !start.HasValue || trade.ClosedDate!.Value >= start.Value)
            .Where(trade => !end.HasValue || trade.ClosedDate!.Value <= end.Value)
            .OrderByDescending(trade => trade.ClosedDate)
            .Take(safeMaxTrades)];

        if (filteredTrades.Count == 0)
        {
            return new TradeAiContextSnapshot(0, "No closed trades matched the requested range.", BuildRangeSummary(start, end));
        }

        Dictionary<int, string> emotionLookup = (await emotionTagProvider.GetEmotionTagsAsync(cancellationToken))
            .GroupBy(tag => tag.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);

        Dictionary<int, string> zoneLookup = (await zoneProvider.GetZonesAsync(cancellationToken))
            .GroupBy(zone => zone.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);

        Dictionary<int, string> technicalLookup = (await technicalAnalysisTagProvider.GetTagsAsync(cancellationToken))
            .GroupBy(tag => tag.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);

        List<string> lines = [.. filteredTrades.Select(trade => BuildTradeLine(trade, emotionLookup, zoneLookup, technicalLookup))];

        return new TradeAiContextSnapshot(
            filteredTrades.Count,
            string.Join("\n", lines),
            BuildRangeSummary(start, end));
    }

    public async Task<TradeAiContextSnapshot> BuildRecentClosedTradesContextAsync(
        int userId,
        int maxTrades,
        CancellationToken cancellationToken)
    {
        int safeMaxTrades = ClampMaxTrades(maxTrades);
        List<TradeCacheDto> trades = await tradeProvider.GetClosedTradesDescendingAsync(userId, safeMaxTrades, cancellationToken);

        if (trades.Count == 0)
        {
            return new TradeAiContextSnapshot(0, "No recently closed trades.", "Most recent closed trades.");
        }

        Dictionary<int, string> emotionLookup = (await emotionTagProvider.GetEmotionTagsAsync(cancellationToken))
            .GroupBy(tag => tag.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);

        Dictionary<int, string> zoneLookup = (await zoneProvider.GetZonesAsync(cancellationToken))
            .GroupBy(zone => zone.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);

        Dictionary<int, string> technicalLookup = (await technicalAnalysisTagProvider.GetTagsAsync(cancellationToken))
            .GroupBy(tag => tag.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);

        List<string> lines = [.. trades.Select(trade => BuildTradeLine(trade, emotionLookup, zoneLookup, technicalLookup))];

        return new TradeAiContextSnapshot(
            trades.Count,
            string.Join("\n", lines),
            "Most recent closed trades.");
    }

    private static string BuildTradeLine(
        TradeCacheDto trade,
        IReadOnlyDictionary<int, string> emotionLookup,
        IReadOnlyDictionary<int, string> zoneLookup,
        IReadOnlyDictionary<int, string> technicalLookup)
    {
        string emotions = JoinResolvedValues(trade.EmotionTags, emotionLookup);
        string technicalThemes = JoinResolvedValues(trade.TechnicalAnalysisTagIds, technicalLookup);
        string zone = trade.TradingZoneId.HasValue && zoneLookup.TryGetValue(trade.TradingZoneId.Value, out string? zoneName)
            ? zoneName
            : "Unknown";
        string closedDate = trade.ClosedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "Open";
        string pnl = trade.Pnl?.ToString("F2", CultureInfo.InvariantCulture) ?? "N/A";

        return $"- {closedDate} | {trade.Asset} | {trade.Position} | PnL: {pnl} | Zone: {zone} | Emotions: {emotions} | Technical: {technicalThemes} | RuleBroken: {(trade.IsRuleBroken ? "Yes" : "No")}";
    }

    private static string JoinResolvedValues(IEnumerable<int>? ids, IReadOnlyDictionary<int, string> lookup)
    {
        if (ids is null)
        {
            return "None";
        }

        List<string> values = [.. ids
            .Where(lookup.ContainsKey)
            .Select(id => lookup[id])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

        return values.Count > 0 ? string.Join(", ", values) : "None";
    }

    private static string BuildRangeSummary(DateTime? start, DateTime? end)
    {
        if (!start.HasValue && !end.HasValue)
        {
            return "All available closed trades.";
        }

        return $"Closed trades from {(start?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "the beginning")} to {(end?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "now")}.";
    }

    private static int ClampMaxTrades(int maxTrades)
    {
        return Math.Clamp(maxTrades, 1, MaxTradeDigestSize);
    }
}