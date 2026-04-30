using TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Modules.Analytics.Features.V1;

public sealed class GetSetupPerformance
{
    internal sealed record Request(AnalyticsFilter Filter, int UserId = 0)
        : IQuery<Result<IReadOnlyCollection<SetupPerformanceViewModel>>>;

    internal sealed record SetupPerformanceViewModel(
        int SetupId,
        string SetupName,
        int TotalTrades,
        int Wins,
        int Losses,
        decimal WinRate,
        decimal TotalPnl,
        decimal AvgPnl,
        decimal AvgWin,
        decimal AvgLoss,
        decimal ProfitFactor,
        decimal Expectancy,
        decimal LargestWin,
        decimal LargestLoss,
        decimal AvgRiskReward,
        decimal AvgHoldingDays,
        string Grade);

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
        ISetupProvider setupProvider) : IQueryHandler<Request, Result<IReadOnlyCollection<SetupPerformanceViewModel>>>
    {
        public async Task<Result<IReadOnlyCollection<SetupPerformanceViewModel>>> Handle(
            Request request, CancellationToken cancellationToken)
        {
            List<TradeCacheDto> trades = await tradeProvider.GetTradesAsync(request.UserId, cancellationToken);
            List<SetupSummaryDto> setups = await setupProvider.GetSetupsAsync(request.UserId, cancellationToken);
            DateTime fromDate = AnalyticsFilterHelper.GetFromDate(request.Filter);

            // Filter to closed trades with a setup assigned
            List<TradeCacheDto> closed = [.. trades
                .Where(t => t.Status == TradeStatus.Closed && t.Pnl.HasValue && t.TradingSetupId.HasValue)
                .Where(t => fromDate == DateTime.MinValue || (t.ClosedDate.HasValue && t.ClosedDate.Value >= fromDate))];

            // Build setup name lookup
            Dictionary<int, string> setupNames = setups.ToDictionary(s => s.Id, s => s.Name);

            // Group by setup and calculate metrics
            var setupGroups = closed
                .GroupBy(t => t.TradingSetupId!.Value)
                .Select(g =>
                {
                    List<TradeCacheDto> groupTrades = [.. g];
                    List<TradeCacheDto> wins = groupTrades.Where(t => t.Pnl > 0).ToList();
                    List<TradeCacheDto> losses = groupTrades.Where(t => t.Pnl <= 0).ToList();

                    decimal totalPnl = groupTrades.Sum(t => t.Pnl!.Value);
                    decimal winRate = (decimal)wins.Count / groupTrades.Count * 100;
                    decimal avgPnl = groupTrades.Average(t => t.Pnl!.Value);
                    decimal avgWin = wins.Count > 0 ? wins.Average(t => t.Pnl!.Value) : 0;
                    decimal avgLoss = losses.Count > 0 ? Math.Abs(losses.Average(t => t.Pnl!.Value)) : 0;

                    decimal grossProfit = wins.Sum(t => t.Pnl!.Value);
                    decimal grossLoss = Math.Abs(losses.Sum(t => t.Pnl!.Value));
                    decimal profitFactor = grossLoss > 0
                        ? grossProfit / grossLoss
                        : (grossProfit > 0 ? decimal.MaxValue : 0);

                    decimal expectancy = (winRate / 100 * avgWin) - ((1 - winRate / 100) * avgLoss);

                    decimal largestWin = wins.Count > 0 ? wins.Max(t => t.Pnl!.Value) : 0;
                    decimal largestLoss = losses.Count > 0 ? losses.Min(t => t.Pnl!.Value) : 0;

                    // Avg risk-reward
                    double[] rrValues = groupTrades
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

                    // Avg holding days
                    double[] holdingDays = groupTrades
                        .Where(t => t.ClosedDate.HasValue)
                        .Select(t => (t.ClosedDate!.Value - t.Date).TotalDays)
                        .ToArray();
                    decimal avgHoldingDays = holdingDays.Length > 0 ? (decimal)holdingDays.Average() : 0;

                    // Grade: A (>65% WR & PF>2), B (>55% WR & PF>1.5), C (>45% WR & PF>1), D (<45% WR), F (<35% WR)
                    string grade = CalculateGrade(winRate, profitFactor, groupTrades.Count);

                    string setupName = setupNames.GetValueOrDefault(g.Key, $"Setup #{g.Key}");

                    return new SetupPerformanceViewModel(
                        g.Key,
                        setupName,
                        groupTrades.Count,
                        wins.Count,
                        losses.Count,
                        Math.Round(winRate, 1),
                        Math.Round(totalPnl, 2),
                        Math.Round(avgPnl, 2),
                        Math.Round(avgWin, 2),
                        Math.Round(avgLoss, 2),
                        Math.Round(profitFactor, 2),
                        Math.Round(expectancy, 2),
                        Math.Round(largestWin, 2),
                        Math.Round(largestLoss, 2),
                        Math.Round(avgRiskReward, 2),
                        Math.Round(avgHoldingDays, 1),
                        grade);
                })
                .OrderByDescending(s => s.TotalPnl)
                .ToList();

            return Result<IReadOnlyCollection<SetupPerformanceViewModel>>.Success(setupGroups);
        }

        private static string CalculateGrade(decimal winRate, decimal profitFactor, int totalTrades)
        {
            // Require at least 5 trades for a meaningful grade
            if (totalTrades < 5)
                return "N/A";

            if (winRate >= 65 && profitFactor >= 2)
                return "A";
            if (winRate >= 55 && profitFactor >= 1.5m)
                return "B";
            if (winRate >= 45 && profitFactor >= 1)
                return "C";
            if (winRate >= 35)
                return "D";

            return "F";
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/analytics");

            group.MapGet("/setup-performance", async (AnalyticsFilter filter, ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request(filter) with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result<IReadOnlyCollection<SetupPerformanceViewModel>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get setup performance breakdown.")
            .WithDescription("Retrieves performance metrics grouped by trading setup, including win rate, PnL, profit factor, expectancy, and an auto-calculated grade.")
            .WithTags(Tags.Analytics)
            .RequireAuthorization();
        }
    }
}
