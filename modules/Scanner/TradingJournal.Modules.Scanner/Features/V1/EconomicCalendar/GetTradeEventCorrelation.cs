using TradingJournal.Modules.Scanner.Dto;
using TradingJournal.Modules.Scanner.Services.EconomicCalendar;
using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Modules.Scanner.Features.V1.EconomicCalendar;

/// <summary>
/// Analyzes the correlation between a trader's closed trades and high-impact economic events.
/// Reveals patterns like "Your worst trades happen within 30 min of NFP releases."
/// </summary>
public sealed class GetTradeEventCorrelation
{
    public record Query() : IQuery<Result<TradeEventCorrelationDto>>
    {
        public int UserId { get; set; }

        /// <summary>
        /// Proximity window in minutes. Trades opened within this window of a high-impact event
        /// are classified as "near events". Default: 30 minutes.
        /// </summary>
        public int ProximityMinutes { get; set; } = 30;
    }

    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.ProximityMinutes)
                .InclusiveBetween(5, 120)
                .WithMessage("Proximity window must be between 5 and 120 minutes.");
        }
    }

    internal sealed class Handler(
        ITradeProvider tradeProvider,
        IEconomicCalendarProvider calendarProvider)
        : IQueryHandler<Query, Result<TradeEventCorrelationDto>>
    {
        public async Task<Result<TradeEventCorrelationDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            // Get all closed trades
            List<TradeCacheDto> allTrades = await tradeProvider.GetTradesAsync(request.UserId, cancellationToken);

            List<TradeCacheDto> closedTrades = allTrades
                .Where(t => t.Status == TradeStatus.Closed && t.Pnl.HasValue && t.ClosedDate.HasValue)
                .OrderBy(t => t.Date)
                .ToList();

            if (closedTrades.Count == 0)
            {
                return Result<TradeEventCorrelationDto>.Success(CreateEmptyResult());
            }

            // Get the date range of trades
            DateOnly earliestDate = DateOnly.FromDateTime(closedTrades.Min(t => t.Date));
            DateOnly latestDate = DateOnly.FromDateTime(closedTrades.Max(t => t.Date));

            // Fetch economic events for the trade period
            // Note: Forex Factory feed only provides current week. For historical correlation,
            // we use what's available in the cache (this week's events).
            List<EconomicEvent> events = await calendarProvider.GetEventsAsync(
                earliestDate, latestDate, cancellationToken);

            // Filter to high-impact events only
            List<EconomicEvent> highImpactEvents = events
                .Where(e => e.Impact == EconomicImpact.High)
                .ToList();

            TimeSpan proximityWindow = TimeSpan.FromMinutes(request.ProximityMinutes);

            // Classify trades as "near events" or "away from events"
            var tradesNearEvents = new List<(TradeCacheDto Trade, EconomicEvent Event, int MinutesFromEvent)>();
            var tradesAwayFromEvents = new List<TradeCacheDto>();

            foreach (TradeCacheDto trade in closedTrades)
            {
                EconomicEvent? nearestEvent = FindNearestEvent(trade.Date, highImpactEvents, proximityWindow);

                if (nearestEvent is not null)
                {
                    int minutesFrom = (int)(trade.Date - nearestEvent.EventDateUtc).TotalMinutes;
                    tradesNearEvents.Add((trade, nearestEvent, minutesFrom));
                }
                else
                {
                    tradesAwayFromEvents.Add(trade);
                }
            }

            // Calculate statistics
            decimal winRateNear = CalculateWinRate(tradesNearEvents.Select(t => t.Trade).ToList());
            decimal winRateAway = CalculateWinRate(tradesAwayFromEvents);
            decimal avgPnlNear = tradesNearEvents.Count > 0
                ? tradesNearEvents.Average(t => t.Trade.Pnl!.Value) : 0;
            decimal avgPnlAway = tradesAwayFromEvents.Count > 0
                ? tradesAwayFromEvents.Average(t => t.Pnl!.Value) : 0;
            decimal totalPnlNear = tradesNearEvents.Sum(t => t.Trade.Pnl!.Value);
            decimal totalPnlAway = tradesAwayFromEvents.Sum(t => t.Pnl!.Value);

            // Event type breakdown
            List<EventTypeCorrelationDto> eventTypeBreakdown = tradesNearEvents
                .GroupBy(t => t.Event.EventName)
                .Select(g =>
                {
                    var trades = g.Select(x => x.Trade).ToList();
                    int wins = trades.Count(t => t.Pnl > 0);
                    int losses = trades.Count(t => t.Pnl <= 0);
                    return new EventTypeCorrelationDto(
                        g.Key,
                        trades.Count,
                        wins,
                        losses,
                        trades.Count > 0 ? Math.Round((decimal)wins / trades.Count * 100, 1) : 0,
                        trades.Count > 0 ? Math.Round(trades.Average(t => t.Pnl!.Value), 2) : 0,
                        Math.Round(trades.Sum(t => t.Pnl!.Value), 2)
                    );
                })
                .OrderByDescending(e => e.TradeCount)
                .ToList();

            // Currency breakdown
            List<CurrencyCorrelationDto> currencyBreakdown = tradesNearEvents
                .GroupBy(t => t.Event.Currency)
                .Select(g =>
                {
                    var trades = g.Select(x => x.Trade).ToList();
                    int wins = trades.Count(t => t.Pnl > 0);
                    return new CurrencyCorrelationDto(
                        g.Key,
                        trades.Count,
                        trades.Count > 0 ? Math.Round((decimal)wins / trades.Count * 100, 1) : 0,
                        trades.Count > 0 ? Math.Round(trades.Average(t => t.Pnl!.Value), 2) : 0,
                        Math.Round(trades.Sum(t => t.Pnl!.Value), 2)
                    );
                })
                .OrderByDescending(c => c.TradeCount)
                .ToList();

            // Proximity breakdown (before vs after the event)
            List<ProximityBucketDto> proximityBreakdown = BuildProximityBreakdown(tradesNearEvents);

            // Generate summary
            string summary = GenerateSummary(
                closedTrades.Count, tradesNearEvents.Count, tradesAwayFromEvents.Count,
                winRateNear, winRateAway, avgPnlNear, avgPnlAway, eventTypeBreakdown);

            var dto = new TradeEventCorrelationDto(
                closedTrades.Count,
                tradesNearEvents.Count,
                tradesAwayFromEvents.Count,
                winRateNear,
                winRateAway,
                Math.Round(avgPnlNear, 2),
                Math.Round(avgPnlAway, 2),
                Math.Round(totalPnlNear, 2),
                Math.Round(totalPnlAway, 2),
                eventTypeBreakdown,
                currencyBreakdown,
                proximityBreakdown,
                summary
            );

            return Result<TradeEventCorrelationDto>.Success(dto);
        }

        private static EconomicEvent? FindNearestEvent(
            DateTime tradeDate, List<EconomicEvent> events, TimeSpan window)
        {
            return events
                .Where(e => Math.Abs((tradeDate - e.EventDateUtc).TotalMinutes) <= window.TotalMinutes)
                .MinBy(e => Math.Abs((tradeDate - e.EventDateUtc).TotalMinutes));
        }

        private static decimal CalculateWinRate(List<TradeCacheDto> trades)
        {
            if (trades.Count == 0) return 0;
            int wins = trades.Count(t => t.Pnl > 0);
            return Math.Round((decimal)wins / trades.Count * 100, 1);
        }

        private static List<ProximityBucketDto> BuildProximityBreakdown(
            List<(TradeCacheDto Trade, EconomicEvent Event, int MinutesFromEvent)> tradesNear)
        {
            var buckets = new[]
            {
                ("30-15 min before", -30, -16),
                ("15-0 min before", -15, -1),
                ("0-15 min after", 0, 15),
                ("15-30 min after", 16, 30),
            };

            return buckets.Select(b =>
            {
                var trades = tradesNear
                    .Where(t => t.MinutesFromEvent >= b.Item2 && t.MinutesFromEvent <= b.Item3)
                    .Select(t => t.Trade)
                    .ToList();

                int wins = trades.Count(t => t.Pnl > 0);

                return new ProximityBucketDto(
                    b.Item1,
                    (b.Item2 + b.Item3) / 2,
                    trades.Count,
                    trades.Count > 0 ? Math.Round((decimal)wins / trades.Count * 100, 1) : 0,
                    trades.Count > 0 ? Math.Round(trades.Average(t => t.Pnl!.Value), 2) : 0
                );
            }).ToList();
        }

        private static string GenerateSummary(
            int total, int nearCount, int awayCount,
            decimal winRateNear, decimal winRateAway,
            decimal avgPnlNear, decimal avgPnlAway,
            List<EventTypeCorrelationDto> eventTypes)
        {
            if (nearCount == 0)
                return "No trades found near high-impact economic events in this period. " +
                       "This is generally good risk management!";

            var parts = new List<string>();

            decimal pctNear = total > 0 ? Math.Round((decimal)nearCount / total * 100, 0) : 0;
            parts.Add($"{pctNear}% of your trades ({nearCount}/{total}) were opened near high-impact news events.");

            if (winRateNear < winRateAway - 5)
            {
                parts.Add($"⚠️ Your win rate drops from {winRateAway}% to {winRateNear}% when trading near news. " +
                           "Consider avoiding trades around major releases.");
            }
            else if (winRateNear > winRateAway + 5)
            {
                parts.Add($"Your win rate actually improves near news ({winRateNear}% vs {winRateAway}%). " +
                           "You may have an edge in volatile conditions.");
            }

            if (avgPnlNear < 0 && avgPnlAway > 0)
            {
                parts.Add($"💡 You lose an average of ${Math.Abs(avgPnlNear):F2} per trade near events " +
                           $"but gain ${avgPnlAway:F2} otherwise. News trading is hurting your P&L.");
            }

            EventTypeCorrelationDto? worstEvent = eventTypes.Where(e => e.TradeCount >= 2)
                .MinBy(e => e.AvgPnl);
            if (worstEvent is not null && worstEvent.AvgPnl < 0)
            {
                parts.Add($"📉 Your worst-performing event type is '{worstEvent.EventName}' " +
                           $"({worstEvent.WinRate}% win rate, avg ${worstEvent.AvgPnl:F2} per trade).");
            }

            return string.Join(" ", parts);
        }

        private static TradeEventCorrelationDto CreateEmptyResult() => new(
            0, 0, 0, 0, 0, 0, 0, 0, 0, [], [], [],
            "No closed trades found. Start trading to see correlation analysis."
        );
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.EconomicCalendar);

            group.MapGet("/trade-correlation", async (
                ClaimsPrincipal user,
                ISender sender,
                [FromQuery] int? proximityMinutes) =>
            {
                var query = new Query
                {
                    UserId = user.GetCurrentUserId(),
                    ProximityMinutes = proximityMinutes ?? 30
                };

                Result<TradeEventCorrelationDto> result = await sender.Send(query);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<TradeEventCorrelationDto>>(StatusCodes.Status200OK)
            .WithSummary("Get trade-event correlation analysis.")
            .WithDescription("Analyzes how trades perform relative to high-impact economic events. " +
                             "Reveals patterns like 'Your worst trades happen within 30 min of NFP releases.'")
            .WithTags(Tags.EconomicCalendar)
            .RequireAuthorization();
        }
    }
}
