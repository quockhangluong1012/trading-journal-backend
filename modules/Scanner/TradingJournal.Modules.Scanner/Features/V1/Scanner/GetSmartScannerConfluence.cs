using TradingJournal.Modules.Scanner.Common.Constants;
using TradingJournal.Modules.Scanner.Dto;
using TradingJournal.Modules.Scanner.Services;
using TradingJournal.Modules.Scanner.Services.EconomicCalendar;
using TradingJournal.Modules.Scanner.Services.ICTAnalysis;
using TradingJournal.Modules.Scanner.Services.LiveData;

namespace TradingJournal.Modules.Scanner.Features.V1.Scanner;

public sealed class GetSmartScannerConfluence
{
    public sealed record Query(
        string Symbol,
        int UserId = 0) : IQuery<Result<SmartScannerConfluenceDto>>;

    public sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.Symbol)
                .NotEmpty()
                .MaximumLength(30)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Symbol is required.");
        }
    }

    internal sealed class Handler(
        IScannerDbContext scannerDb,
        ILiveMarketDataProvider liveDataProvider,
        MultiTimeframeAnalyzer analyzer,
        IEconomicCalendarProvider economicCalendarProvider)
        : IQueryHandler<Query, Result<SmartScannerConfluenceDto>>
    {
        private const int CandlesPerTimeframe = 100;

        public async Task<Result<SmartScannerConfluenceDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            ScannerConfig? config = await scannerDb.ScannerConfigs
                .Include(c => c.EnabledPatterns)
                .Include(c => c.EnabledTimeframes)
                .FirstOrDefaultAsync(c => c.UserId == request.UserId, cancellationToken);

            IReadOnlyList<IctPatternType> enabledPatterns = config?.EnabledPatterns.Select(p => p.PatternType).ToList()
                ?? [.. Enum.GetValues<IctPatternType>()];
            IReadOnlyList<ScannerTimeframe> enabledTimeframes = config?.EnabledTimeframes.Select(t => t.Timeframe).ToList()
                ?? [ScannerTimeframe.D1, ScannerTimeframe.H1, ScannerTimeframe.M15, ScannerTimeframe.M5];
            int minConfluenceScore = config?.MinConfluenceScore ?? 1;

            Dictionary<ScannerTimeframe, List<CandleData>> candlesByTimeframe = [];

            foreach (ScannerTimeframe timeframe in enabledTimeframes)
            {
                List<CandleData> candles = await liveDataProvider.GetRecentCandlesAsync(
                    request.Symbol,
                    timeframe,
                    CandlesPerTimeframe,
                    cancellationToken);

                if (candles.Count > 0)
                {
                    candlesByTimeframe[timeframe] = candles;
                }
            }

            List<(DetectedPattern Pattern, int ConfluenceScore)> detections = analyzer.Analyze(
                request.Symbol,
                candlesByTimeframe,
                enabledPatterns)
                .Where(result => result.ConfluenceScore >= minConfluenceScore)
                .ToList();

            List<SmartScannerConfluenceCandidateDto> candidates = [.. detections
                .GroupBy(result => result.Pattern.Type)
                .Select(group => new SmartScannerConfluenceCandidateDto(
                    group.Key.ToString(),
                    group.Max(item => item.ConfluenceScore),
                    [.. group.Select(item => item.Pattern.Timeframe.ToString()).Distinct().OrderBy(name => name)],
                    [.. group
                        .OrderByDescending(item => item.Pattern.DetectedAt)
                        .Select(item => new SmartScannerConfluenceSignalDto(
                            item.Pattern.Timeframe.ToString(),
                            item.Pattern.PriceAtDetection,
                            item.Pattern.ZoneHigh,
                            item.Pattern.ZoneLow,
                            item.Pattern.Description,
                            item.Pattern.DetectedAt))]))
                .OrderByDescending(candidate => candidate.ConfluenceScore)
                .ThenBy(candidate => candidate.PatternType)];

            (string economicRiskState, string economicRiskMessage) = await BuildEconomicRiskOverlayAsync(
                request.Symbol,
                economicCalendarProvider,
                cancellationToken);

            SmartScannerConfluenceDto dto = new(
                request.Symbol.ToUpperInvariant(),
                economicRiskState,
                economicRiskMessage,
                minConfluenceScore,
                candidates.Count > 0 ? candidates.Max(candidate => candidate.ConfluenceScore) : 0,
                candidates);

            return Result<SmartScannerConfluenceDto>.Success(dto);
        }

        private static async Task<(string RiskState, string Message)> BuildEconomicRiskOverlayAsync(
            string symbol,
            IEconomicCalendarProvider economicCalendarProvider,
            CancellationToken cancellationToken)
        {
            DateTime now = DateTime.UtcNow;
            List<string> currencies = ExtractCurrencies(symbol);

            if (currencies.Count == 0)
            {
                return ("Unavailable", "Economic-event overlay is currently available only for FX pairs.");
            }

            List<EconomicEvent> todayEvents = await economicCalendarProvider.GetTodayEventsAsync(cancellationToken);
            List<EconomicEvent> relevantEvents = [.. todayEvents
                .Where(e => e.Impact == EconomicImpact.High)
                .Where(e => currencies.Count == 0 || currencies.Contains(e.Currency, StringComparer.OrdinalIgnoreCase))];

            EconomicEvent? recentEvent = relevantEvents
                .Where(e => e.EventDateUtc <= now && e.EventDateUtc >= now.AddMinutes(-15))
                .OrderByDescending(e => e.EventDateUtc)
                .FirstOrDefault();

            if (recentEvent is not null)
            {
                int minutesAgo = (int)(now - recentEvent.EventDateUtc).TotalMinutes;
                return ("Red", $"Recent high-impact release: {recentEvent.EventName} ({recentEvent.Currency}) {minutesAgo}m ago.");
            }

            EconomicEvent? nextEvent = relevantEvents
                .Where(e => e.EventDateUtc > now)
                .OrderBy(e => e.EventDateUtc)
                .FirstOrDefault();

            if (nextEvent is null)
            {
                return ("Green", "No relevant high-impact economic event in the current session.");
            }

            int minutesUntil = (int)Math.Ceiling((nextEvent.EventDateUtc - now).TotalMinutes);

            if (minutesUntil <= 30)
            {
                return ("Red", $"High-impact event {nextEvent.EventName} ({nextEvent.Currency}) in {minutesUntil}m.");
            }

            if (minutesUntil <= 60)
            {
                return ("Yellow", $"High-impact event {nextEvent.EventName} ({nextEvent.Currency}) in {minutesUntil}m.");
            }

            return ("Green", $"Next relevant high-impact event is {nextEvent.EventName} in {minutesUntil}m.");
        }

        private static List<string> ExtractCurrencies(string symbol)
        {
            string normalized = symbol.ToUpperInvariant().Replace("/", string.Empty).Replace("-", string.Empty);
            return normalized.Length >= 6 ? [normalized[..3], normalized[3..6]] : [];
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.SmartConfluence);

            group.MapGet("/", async (ISender sender, ClaimsPrincipal user, [FromQuery] string symbol) =>
            {
                Result<SmartScannerConfluenceDto> result = await sender.Send(new Query(symbol, user.GetCurrentUserId()));

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<SmartScannerConfluenceDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get smart multi-timeframe scanner confluence for a symbol.")
            .WithDescription("Runs the scanner analyzers for one symbol, groups multi-timeframe confirmations, and overlays nearby economic-event risk.")
            .WithTags(Tags.SmartConfluence)
            .RequireAuthorization();
        }
    }
}