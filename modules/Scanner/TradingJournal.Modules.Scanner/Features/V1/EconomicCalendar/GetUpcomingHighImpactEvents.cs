using TradingJournal.Modules.Scanner.Dto;
using TradingJournal.Modules.Scanner.Services.EconomicCalendar;

namespace TradingJournal.Modules.Scanner.Features.V1.EconomicCalendar;

public sealed class GetUpcomingHighImpactEvents
{
    public record Query() : IQuery<Result<UpcomingHighImpactDto>>
    {
        public int UserId { get; set; }

        /// <summary>
        /// How many minutes ahead to look (defaults to 30 minutes).
        /// </summary>
        public int LookAheadMinutes { get; set; } = 30;
    }

    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.LookAheadMinutes)
                .InclusiveBetween(5, 1440)
                .WithMessage("Look-ahead window must be between 5 minutes and 24 hours.");
        }
    }

    internal sealed class Handler(IEconomicCalendarProvider calendarProvider)
        : IQueryHandler<Query, Result<UpcomingHighImpactDto>>
    {
        public async Task<Result<UpcomingHighImpactDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            TimeSpan window = TimeSpan.FromMinutes(request.LookAheadMinutes);

            List<EconomicEvent> upcomingEvents =
                await calendarProvider.GetUpcomingHighImpactEventsAsync(window, cancellationToken);

            DateTime now = DateTime.UtcNow;

            var eventDtos = upcomingEvents.Select(e =>
            {
                int minutesUntil = (int)Math.Ceiling((e.EventDateUtc - now).TotalMinutes);

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
                    IsUpcoming: true,
                    MinutesUntilRelease: minutesUntil);
            }).ToList();

            // Determine if the user should stop trading
            // Stop trading if any high-impact event is within 15 minutes
            bool shouldStopTrading = upcomingEvents.Any(e =>
                (e.EventDateUtc - now).TotalMinutes <= 15);

            EconomicEvent? nextEvent = upcomingEvents.MinBy(e => e.EventDateUtc);
            string? nextEventName = nextEvent?.EventName;
            int? minutesUntilNext = nextEvent is not null
                ? (int)Math.Ceiling((nextEvent.EventDateUtc - now).TotalMinutes)
                : null;

            var dto = new UpcomingHighImpactDto(
                upcomingEvents.Count,
                shouldStopTrading,
                nextEventName,
                minutesUntilNext,
                eventDtos);

            return Result<UpcomingHighImpactDto>.Success(dto);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.EconomicCalendar);

            group.MapGet("/upcoming", async (
                ClaimsPrincipal user,
                ISender sender,
                [FromQuery] int? minutes) =>
            {
                var query = new Query
                {
                    UserId = user.GetCurrentUserId(),
                    LookAheadMinutes = minutes ?? 30
                };

                Result<UpcomingHighImpactDto> result = await sender.Send(query);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<UpcomingHighImpactDto>>(StatusCodes.Status200OK)
            .WithSummary("Get upcoming high-impact economic events.")
            .WithDescription("Returns high-impact events within the specified look-ahead window. Includes a 'shouldStopTrading' flag when events are within 15 minutes.")
            .WithTags(Tags.EconomicCalendar)
            .RequireAuthorization();
        }
    }
}
