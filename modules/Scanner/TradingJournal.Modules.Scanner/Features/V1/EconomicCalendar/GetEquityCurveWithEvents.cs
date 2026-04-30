using TradingJournal.Modules.Scanner.Dto;
using TradingJournal.Modules.Scanner.Services.EconomicCalendar;
using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Modules.Scanner.Features.V1.EconomicCalendar;

public sealed class GetEquityCurveWithEvents
{
    public record Query() : IQuery<Result<EquityCurveWithEventsDto>>
    {
        public int UserId { get; set; }
    }

    internal sealed class Handler(
        ITradeProvider tradeProvider,
        IEconomicCalendarProvider calendarProvider)
        : IQueryHandler<Query, Result<EquityCurveWithEventsDto>>
    {
        public async Task<Result<EquityCurveWithEventsDto>> Handle(
            Query request, CancellationToken ct)
        {
            List<TradeCacheDto> trades = await tradeProvider.GetTradesAsync(request.UserId, ct);

            List<TradeCacheDto> closed = trades
                .Where(t => t.Status == TradeStatus.Closed && t.Pnl.HasValue && t.ClosedDate.HasValue)
                .OrderBy(t => t.ClosedDate!.Value)
                .ToList();

            if (closed.Count == 0)
            {
                return Result<EquityCurveWithEventsDto>.Success(
                    new EquityCurveWithEventsDto([], [], 0, 0));
            }

            DateOnly from = DateOnly.FromDateTime(closed.Min(t => t.ClosedDate!.Value));
            DateOnly to = DateOnly.FromDateTime(closed.Max(t => t.ClosedDate!.Value));

            List<EconomicEvent> events = await calendarProvider.GetEventsAsync(from, to, ct);
            List<EconomicEvent> highEvents = events.Where(e => e.Impact == EconomicImpact.High).ToList();

            // Build equity points with event markers
            decimal cumProfit = 0;
            var points = new List<EquityEventOverlayPointDto>();

            foreach (TradeCacheDto t in closed)
            {
                cumProfit += t.Pnl!.Value;
                DateOnly tradeDate = DateOnly.FromDateTime(t.ClosedDate!.Value);

                var markers = highEvents
                    .Where(e => DateOnly.FromDateTime(e.EventDateUtc) == tradeDate)
                    .Select(e => new EventMarkerDto(
                        e.EventName, e.Currency, e.Impact.ToString(),
                        e.EventDateUtc, e.Actual, e.Forecast, e.Previous))
                    .ToList();

                points.Add(new EquityEventOverlayPointDto(
                    t.ClosedDate!.Value, Math.Round(cumProfit, 2), markers));
            }

            // Deduplicate markers for the full list
            var allMarkers = highEvents
                .Select(e => new EventMarkerDto(
                    e.EventName, e.Currency, e.Impact.ToString(),
                    e.EventDateUtc, e.Actual, e.Forecast, e.Previous))
                .ToList();

            return Result<EquityCurveWithEventsDto>.Success(
                new EquityCurveWithEventsDto(points, allMarkers, closed.Count, highEvents.Count));
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGroup(ApiGroup.V1.EconomicCalendar)
                .MapGet("/equity-overlay", async (ClaimsPrincipal user, ISender sender) =>
                {
                    var r = await sender.Send(new Query { UserId = user.GetCurrentUserId() });
                    return r.IsSuccess ? Results.Ok(r) : Results.BadRequest(r);
                })
                .Produces<Result<EquityCurveWithEventsDto>>(StatusCodes.Status200OK)
                .WithSummary("Equity curve with economic event overlay.")
                .WithDescription("Returns equity curve data with high-impact event markers for overlay visualization.")
                .WithTags(Tags.EconomicCalendar)
                .RequireAuthorization();
        }
    }
}
