using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Modules.Scanner.Services.EconomicCalendar;

internal sealed class EconomicImpactContextProvider(
    ITradeProvider tradeProvider,
    IEconomicCalendarProvider calendarProvider) : IEconomicImpactContextProvider
{
    private static readonly TimeSpan RedZone = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan YellowZone = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan PostRelease = TimeSpan.FromMinutes(15);

    public async Task<EconomicImpactContextDto> GetEconomicImpactContextAsync(
        int userId,
        string symbol,
        int proximityMinutes,
        CancellationToken cancellationToken = default)
    {
        string normalizedSymbol = symbol.Trim().ToUpperInvariant();
        List<string> currencies = ExtractCurrencies(normalizedSymbol);
        DateTime now = DateTime.UtcNow;

        if (currencies.Count == 0)
        {
            return new EconomicImpactContextDto(
                normalizedSymbol,
                "Unavailable",
                "Economic impact prediction is currently available only for FX pairs.",
                null,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                "Symbol-specific macro-event correlation is currently supported only for FX pairs.",
                []);
        }

        List<EconomicEvent> todayEvents = await calendarProvider.GetTodayEventsAsync(cancellationToken);
        List<EconomicEvent> highImpactToday = [.. todayEvents.Where(e => e.Impact == EconomicImpact.High)];

        if (currencies.Count > 0)
        {
            highImpactToday = [.. highImpactToday
                .Where(e => currencies.Contains(e.Currency, StringComparer.OrdinalIgnoreCase))];
        }

        List<EconomicEvent> upcoming = [.. highImpactToday
            .Where(e => e.EventDateUtc > now && e.EventDateUtc <= now + YellowZone)
            .OrderBy(e => e.EventDateUtc)];
        List<EconomicEvent> recent = [.. highImpactToday
            .Where(e => e.EventDateUtc <= now && e.EventDateUtc >= now - PostRelease)
            .OrderByDescending(e => e.EventDateUtc)];

        (string safetyLevel, string safetyMessage, int recommendedWaitMinutes) = BuildSafetyState(now, upcoming, recent);

        List<TradeCacheDto> allTrades = await tradeProvider.GetTradesAsync(userId, cancellationToken);
        DateTime currentWeekStart = now.Date.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday);
        if (now.DayOfWeek == DayOfWeek.Sunday)
        {
            currentWeekStart = currentWeekStart.AddDays(-7);
        }

        List<TradeCacheDto> closedTrades = [.. allTrades
            .Where(t => t.Status == TradeStatus.Closed && t.Pnl.HasValue && t.ClosedDate.HasValue)
            .Where(t => t.Date >= currentWeekStart)
            .OrderBy(t => t.Date)];

        if (closedTrades.Count == 0)
        {
            return new EconomicImpactContextDto(
                normalizedSymbol,
                safetyLevel,
                safetyMessage,
                upcoming.Count > 0 ? (int)Math.Ceiling((upcoming[0].EventDateUtc - now).TotalMinutes) : null,
                recommendedWaitMinutes,
                0,
                0,
                0,
                0,
                0,
                0,
                "No current-week closed FX trades found. This predictor currently compares only against the current week because that is the event history available from the calendar provider.",
                MapUpcomingEvents(upcoming, now));
        }

            DateOnly earliestDate = DateOnly.FromDateTime(currentWeekStart);
            DateOnly latestDate = DateOnly.FromDateTime(now);
        List<EconomicEvent> historicalEvents = await calendarProvider.GetEventsAsync(earliestDate, latestDate, cancellationToken);

        List<EconomicEvent> relevantHistoricalEvents = [.. historicalEvents.Where(e => e.Impact == EconomicImpact.High)];
        if (currencies.Count > 0)
        {
            relevantHistoricalEvents = [.. relevantHistoricalEvents
                .Where(e => currencies.Contains(e.Currency, StringComparer.OrdinalIgnoreCase))];
        }

        TimeSpan proximityWindow = TimeSpan.FromMinutes(proximityMinutes);
        List<TradeCacheDto> tradesNearEvents = [];
        List<TradeCacheDto> tradesAwayFromEvents = [];

        foreach (TradeCacheDto trade in closedTrades)
        {
            EconomicEvent? nearestEvent = relevantHistoricalEvents
                .Where(e => Math.Abs((trade.Date - e.EventDateUtc).TotalMinutes) <= proximityWindow.TotalMinutes)
                .MinBy(e => Math.Abs((trade.Date - e.EventDateUtc).TotalMinutes));

            if (nearestEvent is null)
            {
                tradesAwayFromEvents.Add(trade);
                continue;
            }

            tradesNearEvents.Add(trade);
        }

        decimal winRateNear = CalculateWinRate(tradesNearEvents);
        decimal winRateAway = CalculateWinRate(tradesAwayFromEvents);
        decimal avgPnlNear = tradesNearEvents.Count > 0 ? tradesNearEvents.Average(t => t.Pnl!.Value) : 0;
        decimal avgPnlAway = tradesAwayFromEvents.Count > 0 ? tradesAwayFromEvents.Average(t => t.Pnl!.Value) : 0;

        return new EconomicImpactContextDto(
            normalizedSymbol,
            safetyLevel,
            safetyMessage,
            upcoming.Count > 0 ? (int)Math.Ceiling((upcoming[0].EventDateUtc - now).TotalMinutes) : null,
            recommendedWaitMinutes,
            tradesNearEvents.Count,
            tradesAwayFromEvents.Count,
            winRateNear,
            winRateAway,
            Math.Round(avgPnlNear, 2),
            Math.Round(avgPnlAway, 2),
            BuildCorrelationSummary(normalizedSymbol, proximityMinutes, tradesNearEvents, tradesAwayFromEvents, winRateNear, winRateAway, avgPnlNear, avgPnlAway),
            MapUpcomingEvents(upcoming, now));
    }

    private static (string SafetyLevel, string SafetyMessage, int RecommendedWaitMinutes) BuildSafetyState(
        DateTime now,
        IReadOnlyList<EconomicEvent> upcoming,
        IReadOnlyList<EconomicEvent> recent)
    {
        bool isRed = upcoming.Any(e => e.EventDateUtc <= now + RedZone) || recent.Count > 0;
        bool isYellow = !isRed && upcoming.Count > 0;

        if (isRed)
        {
            if (recent.Count > 0 && !upcoming.Any(e => e.EventDateUtc <= now + RedZone))
            {
                EconomicEvent recentEvent = recent[0];
                int minutesAgo = (int)(now - recentEvent.EventDateUtc).TotalMinutes;
                int waitMinutes = (int)PostRelease.TotalMinutes - minutesAgo;
                return ("Red", $"{recentEvent.EventName} ({recentEvent.Currency}) released {minutesAgo}m ago. Wait {waitMinutes}m.", waitMinutes);
            }

            EconomicEvent nextEvent = upcoming[0];
            int minutesUntil = (int)Math.Ceiling((nextEvent.EventDateUtc - now).TotalMinutes);
            int recommendedWaitMinutes = minutesUntil + (int)PostRelease.TotalMinutes;
            return ("Red", $"{nextEvent.EventName} ({nextEvent.Currency}) is in {minutesUntil}m. Wait about {recommendedWaitMinutes}m.", recommendedWaitMinutes);
        }

        if (isYellow)
        {
            EconomicEvent nextEvent = upcoming[0];
            int minutesUntil = (int)Math.Ceiling((nextEvent.EventDateUtc - now).TotalMinutes);
            return ("Yellow", $"{nextEvent.EventName} ({nextEvent.Currency}) is in {minutesUntil}m. Reduce size and be selective.", 0);
        }

        EconomicEvent? futureEvent = upcoming.Count > 0 ? upcoming[0] : null;
        return futureEvent is not null
            ? ("Green", $"No immediate event risk. Next relevant event is {futureEvent.EventName} in {(int)Math.Ceiling((futureEvent.EventDateUtc - now).TotalMinutes)}m.", 0)
            : ("Green", "No more relevant high-impact events in the current look-ahead window.", 0);
    }

    private static List<EconomicImpactEventDto> MapUpcomingEvents(IReadOnlyList<EconomicEvent> events, DateTime now)
    {
        return [.. events.Take(5).Select(e => new EconomicImpactEventDto(
            e.Id,
            e.EventName,
            e.Country,
            e.Currency,
            e.Impact.ToString(),
            e.EventDateUtc,
            e.EventDateUtc > now ? (int)Math.Ceiling((e.EventDateUtc - now).TotalMinutes) : null,
            e.Forecast,
            e.Previous))];
    }

    private static List<string> ExtractCurrencies(string symbol)
    {
        string normalized = symbol.Replace("/", string.Empty).Replace("-", string.Empty);
        return normalized.Length >= 6 ? [normalized[..3], normalized[3..6]] : [];
    }

    private static decimal CalculateWinRate(IReadOnlyList<TradeCacheDto> trades)
    {
        if (trades.Count == 0)
        {
            return 0;
        }

        int wins = trades.Count(t => t.Pnl > 0);
        return Math.Round((decimal)wins / trades.Count * 100, 1);
    }

    private static string BuildCorrelationSummary(
        string symbol,
        int proximityMinutes,
        IReadOnlyList<TradeCacheDto> tradesNearEvents,
        IReadOnlyList<TradeCacheDto> tradesAwayFromEvents,
        decimal winRateNear,
        decimal winRateAway,
        decimal avgPnlNear,
        decimal avgPnlAway)
    {
        if (tradesNearEvents.Count == 0)
        {
            return $"No current-week closed {symbol} trades were found within {proximityMinutes} minutes of relevant high-impact events.";
        }

        string headline = $"{tradesNearEvents.Count} current-week closed {symbol} trades were opened near relevant high-impact events.";

        if (tradesAwayFromEvents.Count == 0)
        {
            return $"{headline} There is not enough non-event trade history yet for a clean comparison.";
        }

        if (winRateNear < winRateAway)
        {
            return $"{headline} Your win rate drops from {winRateAway}% away from events to {winRateNear}% near them, and average PnL changes from {avgPnlAway:F2} to {avgPnlNear:F2}.";
        }

        if (winRateNear > winRateAway)
        {
            return $"{headline} Your win rate improves from {winRateAway}% away from events to {winRateNear}% near them, but confirm this edge before leaning on it.";
        }

        return $"{headline} Your event and non-event trade win rates are similar ({winRateNear}% vs {winRateAway}%), so sizing and timing discipline matter more than the event itself.";
    }
}