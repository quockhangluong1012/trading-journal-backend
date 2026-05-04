using Microsoft.AspNetCore.Mvc;
using TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Modules.Analytics.Features.V1;

public sealed class CompareSetups
{
    internal sealed record Request(int SetupIdA, int SetupIdB, AnalyticsFilter Filter, int UserId = 0)
        : IQuery<Result<SetupComparisonViewModel>>;

    internal sealed record SetupMetrics(
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

    internal sealed record SetupComparisonViewModel(
        SetupMetrics SetupA,
        SetupMetrics SetupB,
        string Recommendation);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.SetupIdA)
                .GreaterThan(0)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Setup A ID is required.");

            RuleFor(x => x.SetupIdB)
                .GreaterThan(0)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Setup B ID is required.");

            RuleFor(x => x.SetupIdA)
                .NotEqual(x => x.SetupIdB)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Cannot compare a setup with itself.");

            RuleFor(x => x.Filter)
                .IsInEnum()
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Invalid filter value.");
        }
    }

    internal sealed class Handler(
        ITradeProvider tradeProvider,
        ISetupProvider setupProvider) : IQueryHandler<Request, Result<SetupComparisonViewModel>>
    {
        public async Task<Result<SetupComparisonViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            List<TradeCacheDto> trades = await tradeProvider.GetTradesAsync(request.UserId, cancellationToken);
            List<SetupSummaryDto> setups = await setupProvider.GetSetupsAsync(request.UserId, cancellationToken);
            DateTimeOffset fromDate = AnalyticsFilterHelper.GetFromDate(request.Filter);

            Dictionary<int, string> setupNames = setups.ToDictionary(s => s.Id, s => s.Name);

            if (!setupNames.ContainsKey(request.SetupIdA) || !setupNames.ContainsKey(request.SetupIdB))
            {
                return Result<SetupComparisonViewModel>.Failure(Error.Create("One or both setups not found."));
            }

            List<TradeCacheDto> closed = [.. trades
                .Where(t => t.Status == TradeStatus.Closed && t.Pnl.HasValue && t.TradingSetupId.HasValue)
                .Where(t => fromDate == DateTimeOffset.MinValue || (t.ClosedDate.HasValue && t.ClosedDate.Value >= fromDate))];

            SetupMetrics metricsA = CalculateMetrics(request.SetupIdA, setupNames[request.SetupIdA], closed);
            SetupMetrics metricsB = CalculateMetrics(request.SetupIdB, setupNames[request.SetupIdB], closed);

            string recommendation = GenerateRecommendation(metricsA, metricsB);

            return Result<SetupComparisonViewModel>.Success(new SetupComparisonViewModel(metricsA, metricsB, recommendation));
        }

        private static SetupMetrics CalculateMetrics(int setupId, string setupName, List<TradeCacheDto> allClosed)
        {
            List<TradeCacheDto> groupTrades = allClosed.Where(t => t.TradingSetupId == setupId).ToList();

            if (groupTrades.Count == 0)
            {
                return new SetupMetrics(setupId, setupName, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "N/A");
            }

            List<TradeCacheDto> wins = groupTrades.Where(t => t.Pnl > 0).ToList();
            List<TradeCacheDto> losses = groupTrades.Where(t => t.Pnl <= 0).ToList();

            decimal totalPnl = groupTrades.Sum(t => t.Pnl!.Value);
            decimal winRate = (decimal)wins.Count / groupTrades.Count * 100;
            decimal avgPnl = groupTrades.Average(t => t.Pnl!.Value);
            decimal avgWin = wins.Count > 0 ? wins.Average(t => t.Pnl!.Value) : 0;
            decimal avgLoss = losses.Count > 0 ? Math.Abs(losses.Average(t => t.Pnl!.Value)) : 0;

            decimal grossProfit = wins.Sum(t => t.Pnl!.Value);
            decimal grossLoss = Math.Abs(losses.Sum(t => t.Pnl!.Value));
            decimal profitFactor = grossLoss > 0 ? grossProfit / grossLoss : (grossProfit > 0 ? decimal.MaxValue : 0);

            decimal expectancy = (winRate / 100 * avgWin) - ((1 - winRate / 100) * avgLoss);

            decimal largestWin = wins.Count > 0 ? wins.Max(t => t.Pnl!.Value) : 0;
            decimal largestLoss = losses.Count > 0 ? losses.Min(t => t.Pnl!.Value) : 0;

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

            double[] holdingDays = groupTrades
                .Where(t => t.ClosedDate.HasValue)
                .Select(t => (t.ClosedDate!.Value - t.Date).TotalDays)
                .ToArray();
            decimal avgHoldingDays = holdingDays.Length > 0 ? (decimal)holdingDays.Average() : 0;

            string grade = CalculateGrade(winRate, profitFactor, groupTrades.Count);

            return new SetupMetrics(
                setupId, setupName, groupTrades.Count, wins.Count, losses.Count,
                Math.Round(winRate, 1), Math.Round(totalPnl, 2), Math.Round(avgPnl, 2),
                Math.Round(avgWin, 2), Math.Round(avgLoss, 2), Math.Round(profitFactor, 2),
                Math.Round(expectancy, 2), Math.Round(largestWin, 2), Math.Round(largestLoss, 2),
                Math.Round(avgRiskReward, 2), Math.Round(avgHoldingDays, 1), grade);
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

        private static string GenerateRecommendation(SetupMetrics a, SetupMetrics b)
        {
            if (a.TotalTrades < 5 && b.TotalTrades < 5)
                return "Both setups have fewer than 5 trades — not enough data to compare. Keep trading both and revisit.";

            if (a.TotalTrades < 5)
                return $"{b.SetupName} has more data ({b.TotalTrades} trades). Keep gathering data for {a.SetupName} before comparing.";

            if (b.TotalTrades < 5)
                return $"{a.SetupName} has more data ({a.TotalTrades} trades). Keep gathering data for {b.SetupName} before comparing.";

            int scoreA = 0, scoreB = 0;

            if (a.WinRate > b.WinRate) scoreA++; else if (b.WinRate > a.WinRate) scoreB++;
            if (a.ProfitFactor > b.ProfitFactor) scoreA += 2; else if (b.ProfitFactor > a.ProfitFactor) scoreB += 2;
            if (a.Expectancy > b.Expectancy) scoreA += 2; else if (b.Expectancy > a.Expectancy) scoreB += 2;
            if (a.TotalPnl > b.TotalPnl) scoreA++; else if (b.TotalPnl > a.TotalPnl) scoreB++;
            if (a.AvgRiskReward > b.AvgRiskReward) scoreA++; else if (b.AvgRiskReward > a.AvgRiskReward) scoreB++;

            if (scoreA > scoreB)
                return $"{a.SetupName} outperforms {b.SetupName} across key metrics (score {scoreA}-{scoreB}). Consider focusing more on {a.SetupName}.";

            if (scoreB > scoreA)
                return $"{b.SetupName} outperforms {a.SetupName} across key metrics (score {scoreB}-{scoreA}). Consider focusing more on {b.SetupName}.";

            return $"Both setups perform similarly (score {scoreA}-{scoreB}). Consider other factors like trading comfort and market conditions.";
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/analytics");

            group.MapGet("/compare-setups", async (
                [FromQuery] int setupIdA,
                [FromQuery] int setupIdB,
                [FromQuery] AnalyticsFilter filter,
                ClaimsPrincipal user,
                ISender sender) =>
            {
                var result = await sender.Send(
                    new Request(setupIdA, setupIdB, filter) with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result<SetupComparisonViewModel>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Compare two setups side-by-side with performance metrics and recommendation.")
            .WithDescription("Compares win rate, profit factor, expectancy, R:R, and PnL between two setups to help identify which setup to focus on.")
            .WithTags(Tags.Analytics)
            .RequireAuthorization();
        }
    }
}
