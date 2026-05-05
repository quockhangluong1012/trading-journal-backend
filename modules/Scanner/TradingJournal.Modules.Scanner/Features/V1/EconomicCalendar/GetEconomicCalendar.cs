using TradingJournal.Modules.Scanner.Dto;
using TradingJournal.Modules.Scanner.Services.EconomicCalendar;

namespace TradingJournal.Modules.Scanner.Features.V1.EconomicCalendar;

public sealed class GetEconomicCalendar
{
    public record Query() : IQuery<Result<EconomicCalendarDto>>
    {
        public int UserId { get; set; }

        /// <summary>
        /// Start date (defaults to today).
        /// </summary>
        public DateOnly? From { get; set; }

        /// <summary>
        /// End date (defaults to today).
        /// </summary>
        public DateOnly? To { get; set; }

        /// <summary>
        /// Optional impact filter: "High", "Medium", "Low".
        /// If null, returns all impact levels.
        /// </summary>
        public string? ImpactFilter { get; set; }
    }

    internal sealed class Handler(IEconomicCalendarProvider calendarProvider)
        : IQueryHandler<Query, Result<EconomicCalendarDto>>
    {
        public async Task<Result<EconomicCalendarDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            DateOnly from = request.From ?? DateOnly.FromDateTime(DateTime.UtcNow);
            DateOnly to = request.To ?? DateOnly.FromDateTime(DateTime.UtcNow);

            // Limit range to 30 days max
            if (to.DayNumber - from.DayNumber > 30)
            {
                to = from.AddDays(30);
            }

            List<EconomicEvent> events = await calendarProvider.GetEventsAsync(from, to, cancellationToken);

            // Apply impact filter if specified
            if (!string.IsNullOrEmpty(request.ImpactFilter) &&
                Enum.TryParse<EconomicImpact>(request.ImpactFilter, ignoreCase: true, out var impactFilter))
            {
                events = events.Where(e => e.Impact == impactFilter).ToList();
            }

            DateTime now = DateTime.UtcNow;

            var eventDtos = events.Select(e =>
            {
                bool isUpcoming = e.EventDateUtc > now && e.Actual is null;
                int? minutesUntil = isUpcoming
                    ? (int)Math.Ceiling((e.EventDateUtc - now).TotalMinutes)
                    : null;

                return new EconomicEventDto(
                    e.Id,
                    e.Country,
                    e.Currency,
                    e.EventName,
                    e.EventDateUtc,
                    e.Impact.ToString(),
                    e.Actual,
                    e.Forecast,
                    e.Previous,
                    e.Unit,
                    isUpcoming,
                    minutesUntil);
            }).ToList();

            int highCount = events.Count(e => e.Impact == EconomicImpact.High);
            int mediumCount = events.Count(e => e.Impact == EconomicImpact.Medium);
            int lowCount = events.Count(e => e.Impact == EconomicImpact.Low);

            var dto = new EconomicCalendarDto(
                from,
                to,
                events.Count,
                highCount,
                mediumCount,
                lowCount,
                eventDtos);

            return Result<EconomicCalendarDto>.Success(dto);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.EconomicCalendar);

            group.MapGet("/", async (
                ClaimsPrincipal user,
                ISender sender,
                [FromQuery] string? from,
                [FromQuery] string? to,
                [FromQuery] string? impact) =>
            {
                var query = new Query
                {
                    UserId = user.GetCurrentUserId(),
                    From = DateOnly.TryParse(from, out var fromDate) ? fromDate : null,
                    To = DateOnly.TryParse(to, out var toDate) ? toDate : null,
                    ImpactFilter = impact
                };

                Result<EconomicCalendarDto> result = await sender.Send(query);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<EconomicCalendarDto>>(StatusCodes.Status200OK)
            .WithSummary("Get economic calendar events for a date range.")
            .WithDescription("Returns economic events with impact levels. Defaults to today. Max range: 30 days.")
            .WithTags(Tags.EconomicCalendar)
            .RequireAuthorization();
        }
    }
}
