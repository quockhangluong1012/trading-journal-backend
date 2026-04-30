using TradingJournal.Modules.Scanner.Dto;
using TradingJournal.Modules.Scanner.Services.EconomicCalendar;

namespace TradingJournal.Modules.Scanner.Features.V1.EconomicCalendar;

public sealed class GetPreTradeCheck
{
    public record Query() : IQuery<Result<PreTradeCheckDto>>
    {
        public int UserId { get; set; }
        public string? Symbol { get; set; }
    }

    internal sealed class Handler(IEconomicCalendarProvider calendarProvider)
        : IQueryHandler<Query, Result<PreTradeCheckDto>>
    {
        private static readonly TimeSpan RedZone = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan YellowZone = TimeSpan.FromMinutes(60);
        private static readonly TimeSpan PostRelease = TimeSpan.FromMinutes(15);

        public async Task<Result<PreTradeCheckDto>> Handle(Query request, CancellationToken ct)
        {
            DateTime now = DateTime.UtcNow;
            List<EconomicEvent> today = await calendarProvider.GetTodayEventsAsync(ct);
            List<EconomicEvent> high = today.Where(e => e.Impact == EconomicImpact.High).ToList();

            if (!string.IsNullOrWhiteSpace(request.Symbol))
            {
                var currencies = ExtractCurrencies(request.Symbol);
                if (currencies.Count > 0)
                    high = high.Where(e => currencies.Contains(e.Currency, StringComparer.OrdinalIgnoreCase)).ToList();
            }

            var upcoming = high.Where(e => e.EventDateUtc > now && e.EventDateUtc <= now + YellowZone)
                .OrderBy(e => e.EventDateUtc).ToList();
            var recent = high.Where(e => e.EventDateUtc <= now && e.EventDateUtc >= now - PostRelease)
                .OrderByDescending(e => e.EventDateUtc).ToList();

            bool isRed = upcoming.Any(e => e.EventDateUtc <= now + RedZone) || recent.Count > 0;
            bool isYellow = !isRed && upcoming.Count > 0;

            string safetyLevel; string message; int wait;

            if (isRed)
            {
                safetyLevel = "Red";
                if (recent.Count > 0 && !upcoming.Any(e => e.EventDateUtc <= now + RedZone))
                {
                    var r = recent[0];
                    int ago = (int)(now - r.EventDateUtc).TotalMinutes;
                    wait = (int)PostRelease.TotalMinutes - ago;
                    message = $"🔴 STOP — {r.EventName} ({r.Currency}) released {ago}m ago. Wait {wait}m.";
                }
                else
                {
                    var next = upcoming[0];
                    int mins = (int)Math.Ceiling((next.EventDateUtc - now).TotalMinutes);
                    wait = mins + (int)PostRelease.TotalMinutes;
                    message = $"🔴 STOP — {next.EventName} ({next.Currency}) in {mins}m. Wait ~{wait}m.";
                }
            }
            else if (isYellow)
            {
                safetyLevel = "Yellow";
                var next = upcoming[0];
                int mins = (int)Math.Ceiling((next.EventDateUtc - now).TotalMinutes);
                wait = 0;
                message = $"🟡 CAUTION — {next.EventName} ({next.Currency}) in {mins}m. Use tight risk.";
            }
            else
            {
                safetyLevel = "Green"; wait = 0;
                var nextHi = high.Where(e => e.EventDateUtc > now).MinBy(e => e.EventDateUtc);
                message = nextHi is not null
                    ? $"🟢 Safe to trade. Next: {nextHi.EventName} in {(int)Math.Ceiling((nextHi.EventDateUtc - now).TotalMinutes)}m."
                    : "🟢 Safe to trade. No more high-impact events today.";
            }

            int? minsUntilNext = high.Where(e => e.EventDateUtc > now).MinBy(e => e.EventDateUtc)
                is { } n ? (int)Math.Ceiling((n.EventDateUtc - now).TotalMinutes) : null;

            return Result<PreTradeCheckDto>.Success(new PreTradeCheckDto(
                safetyLevel == "Green", safetyLevel, message,
                MapDtos(upcoming, now), MapDtos(recent, now), minsUntilNext, wait));
        }

        private static List<string> ExtractCurrencies(string symbol)
        {
            string s = symbol.ToUpperInvariant().Replace("/", "").Replace("-", "");
            return s.Length >= 6 ? [s[..3], s[3..6]] : [];
        }

        private static List<EconomicEventDto> MapDtos(List<EconomicEvent> events, DateTime now) =>
            events.Select(e => new EconomicEventDto(e.Id, e.Country, e.Currency, e.EventName,
                e.EventDateUtc, e.Impact.ToString(), e.Actual, e.Forecast, e.Previous, e.Unit,
                e.EventDateUtc > now,
                e.EventDateUtc > now ? (int)Math.Ceiling((e.EventDateUtc - now).TotalMinutes) : null
            )).ToList();
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGroup(ApiGroup.V1.EconomicCalendar)
                .MapGet("/pre-trade-check", async (ClaimsPrincipal user, ISender sender,
                    [FromQuery] string? symbol) =>
                {
                    var r = await sender.Send(new Query { UserId = user.GetCurrentUserId(), Symbol = symbol });
                    return r.IsSuccess ? Results.Ok(r) : Results.BadRequest(r);
                })
                .Produces<Result<PreTradeCheckDto>>(StatusCodes.Status200OK)
                .WithSummary("Pre-trade safety check based on economic calendar.")
                .WithDescription("Returns Green/Yellow/Red safety level based on proximity to high-impact events.")
                .WithTags(Tags.EconomicCalendar)
                .RequireAuthorization();
        }
    }
}
