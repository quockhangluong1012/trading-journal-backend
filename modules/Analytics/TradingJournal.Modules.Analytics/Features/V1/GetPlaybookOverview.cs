using TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Modules.Analytics.Features.V1;

public sealed class GetPlaybookOverview
{
    internal sealed record Request(AnalyticsFilter Filter, int UserId = 0)
        : IQuery<Result<PlaybookOverviewViewModel>>;

    internal sealed record PlaybookSetupCard(
        int SetupId,
        string SetupName,
        string? Description,
        int Status,
        int TotalTrades,
        int Wins,
        int Losses,
        decimal WinRate,
        decimal TotalPnl,
        decimal ProfitFactor,
        decimal Expectancy,
        decimal AvgRiskReward,
        string Grade);

    internal sealed record PlaybookOverviewViewModel(
        IReadOnlyCollection<PlaybookSetupCard> Setups,
        int TotalSetups,
        int ActiveSetups,
        int RetiredSetups,
        string? TopSetupName,
        string? WorstSetupName);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Filter)
                .IsInEnum()
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Invalid filter value.");
        }
    }

    internal sealed class Handler(
        ITradeProvider tradeProvider,
        ISetupProvider setupProvider) : IQueryHandler<Request, Result<PlaybookOverviewViewModel>>
    {
        public async Task<Result<PlaybookOverviewViewModel>> Handle(
            Request request, CancellationToken cancellationToken)
        {
            List<TradeCacheDto> trades = await tradeProvider.GetTradesAsync(request.UserId, cancellationToken);
            List<SetupSummaryDto> setups = await setupProvider.GetSetupsAsync(request.UserId, cancellationToken);
            DateTimeOffset fromDate = AnalyticsFilterHelper.GetFromDate(request.Filter);

            List<TradeCacheDto> closed = [.. trades
                .Where(t => t.Status == TradeStatus.Closed && t.Pnl.HasValue && t.TradingSetupId.HasValue)
                .Where(t => fromDate == DateTimeOffset.MinValue || (t.ClosedDate.HasValue && t.ClosedDate.Value >= fromDate))];

            // Build setup cards with performance data
            List<PlaybookSetupCard> cards = [];

            foreach (SetupSummaryDto setup in setups)
            {
                List<TradeCacheDto> setupTrades = closed.Where(t => t.TradingSetupId == setup.Id).ToList();
                List<TradeCacheDto> wins = setupTrades.Where(t => t.Pnl > 0).ToList();
                List<TradeCacheDto> losses = setupTrades.Where(t => t.Pnl <= 0).ToList();

                decimal totalPnl = setupTrades.Count > 0 ? setupTrades.Sum(t => t.Pnl!.Value) : 0;
                decimal winRate = setupTrades.Count > 0 ? (decimal)wins.Count / setupTrades.Count * 100 : 0;

                decimal avgWin = wins.Count > 0 ? wins.Average(t => t.Pnl!.Value) : 0;
                decimal avgLoss = losses.Count > 0 ? Math.Abs(losses.Average(t => t.Pnl!.Value)) : 0;

                decimal grossProfit = wins.Sum(t => t.Pnl!.Value);
                decimal grossLoss = Math.Abs(losses.Sum(t => t.Pnl!.Value));
                decimal profitFactor = grossLoss > 0
                    ? grossProfit / grossLoss
                    : (grossProfit > 0 ? decimal.MaxValue : 0);

                decimal expectancy = (winRate / 100 * avgWin) - ((1 - winRate / 100) * avgLoss);

                double[] rrValues = setupTrades
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

                string grade = CalculateGrade(winRate, profitFactor, setupTrades.Count);

                cards.Add(new PlaybookSetupCard(
                    setup.Id,
                    setup.Name,
                    setup.Description,
                    setup.Status,
                    setupTrades.Count,
                    wins.Count,
                    losses.Count,
                    Math.Round(winRate, 1),
                    Math.Round(totalPnl, 2),
                    Math.Round(profitFactor, 2),
                    Math.Round(expectancy, 2),
                    Math.Round(avgRiskReward, 2),
                    grade));
            }

            // Sort: active first, then by total PnL descending
            cards = [.. cards
                .OrderBy(c => c.Status == 4 ? 1 : 0) // Retired = 4 goes to bottom
                .ThenByDescending(c => c.TotalPnl)];

            int activeSetups = setups.Count(s => s.Status != 4);
            int retiredSetups = setups.Count(s => s.Status == 4);

            PlaybookSetupCard? topSetup = cards
                .Where(c => c.TotalTrades >= 5 && c.Status != 4)
                .OrderByDescending(c => c.Expectancy)
                .FirstOrDefault();

            PlaybookSetupCard? worstSetup = cards
                .Where(c => c.TotalTrades >= 5 && c.Status != 4)
                .OrderBy(c => c.Expectancy)
                .FirstOrDefault();

            return Result<PlaybookOverviewViewModel>.Success(new PlaybookOverviewViewModel(
                cards,
                setups.Count,
                activeSetups,
                retiredSetups,
                topSetup?.SetupName,
                worstSetup?.SetupName != topSetup?.SetupName ? worstSetup?.SetupName : null));
        }

        private static string CalculateGrade(decimal winRate, decimal profitFactor, int totalTrades)
        {
            if (totalTrades < 5) return "N/A";
            if (winRate >= 65 && profitFactor >= 2) return "A";
            if (winRate >= 55 && profitFactor >= 1.5m) return "B";
            if (winRate >= 45 && profitFactor >= 1) return "C";
            if (winRate >= 35) return "D";
            return "F";
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/analytics");

            group.MapGet("/playbook-overview", async (AnalyticsFilter filter, ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request(filter) with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result<PlaybookOverviewViewModel>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get playbook overview with all setups' performance cards and grades.")
            .WithDescription("Returns a summary of all trading setups with their performance metrics, grades, and active/retired status.")
            .WithTags(Tags.Analytics)
            .RequireAuthorization();
        }
    }
}
