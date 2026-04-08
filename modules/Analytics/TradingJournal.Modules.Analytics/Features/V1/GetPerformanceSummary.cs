using TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Modules.Analytics.Features.V1;

public sealed class GetPerformanceSummary
{
    internal sealed record Request(AnalyticsFilter Filter, int UserId = 0) : IQuery<Result<PerformanceSummaryViewModel>>;

    internal sealed record PerformanceSummaryViewModel(
        double TotalPnl,
        double WinRate,
        int Wins,
        int Losses,
        int TotalClosed,
        double AvgWin,
        double AvgLoss,
        double LargestWin,
        double LargestLoss,
        double ProfitFactor,
        double Expectancy,
        double MaxDrawdown,
        double MaxDrawdownPct,
        double SharpeRatio,
        double AvgHoldingDays,
        double LongsWinRate,
        double ShortsWinRate,
        int ConsecutiveWins,
        int ConsecutiveLosses,
        double AvgRiskReward);

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

    internal sealed class Handler(ITradeProvider tradeProvider) : IQueryHandler<Request, Result<PerformanceSummaryViewModel>>
    {
        public async Task<Result<PerformanceSummaryViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            List<TradeCacheDto> allTrades = await tradeProvider.GetTradesAsync(cancellationToken);
            List<TradeCacheDto> trades = [.. allTrades.Where(t => t.CreatedBy == request.UserId)];
            DateTime fromDate = AnalyticsFilterHelper.GetFromDate(request.Filter);

            List<TradeCacheDto> closed = [.. trades
                .Where(t => t.Status == TradeStatus.Closed && t.Pnl.HasValue)
                .Where(t => fromDate == DateTime.MinValue || (t.ClosedDate.HasValue && t.ClosedDate.Value >= fromDate))];

            if (closed.Count == 0)
            {
                return Result<PerformanceSummaryViewModel>.Success(new PerformanceSummaryViewModel(
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
            }

            List<TradeCacheDto> wins = closed.Where(t => t.Pnl > 0).ToList();
            List<TradeCacheDto> losses = closed.Where(t => t.Pnl <= 0).ToList();

            double totalPnl = closed.Sum(t => (double)t.Pnl!.Value);
            double winRate = (double)wins.Count / closed.Count * 100;
            double avgWin = wins.Count > 0 ? wins.Average(t => (double)t.Pnl!.Value) : 0;
            double avgLoss = losses.Count > 0 ? Math.Abs(losses.Average(t => (double)t.Pnl!.Value)) : 0;
            double largestWin = wins.Count > 0 ? (double)wins.Max(t => t.Pnl!.Value) : 0;
            double largestLoss = losses.Count > 0 ? (double)losses.Min(t => t.Pnl!.Value) : 0;

            // Profit factor
            double grossProfit = wins.Sum(t => (double)t.Pnl!.Value);
            double grossLoss = Math.Abs(losses.Sum(t => (double)t.Pnl!.Value));
            double profitFactor = grossLoss > 0 ? grossProfit / grossLoss : (grossProfit > 0 ? double.MaxValue : 0);

            // Expectancy
            double expectancy = (winRate / 100 * avgWin) - ((1 - winRate / 100) * avgLoss);

            // Max drawdown
            List<TradeCacheDto> sorted = closed
                .Where(t => t.ClosedDate.HasValue)
                .OrderBy(t => t.ClosedDate!.Value)
                .ToList();

            double peak = 0, equity = 0, maxDD = 0, maxDDPct = 0;
            foreach (TradeCacheDto t in sorted)
            {
                equity += (double)t.Pnl!.Value;
                if (equity > peak) peak = equity;
                double dd = peak - equity;
                if (dd > maxDD)
                {
                    maxDD = dd;
                    maxDDPct = peak > 0 ? dd / peak * 100 : 0;
                }
            }

            // Sharpe ratio (simplified)
            double[] returns = sorted.Select(t => (double)t.Pnl!.Value).ToArray();
            double meanReturn = returns.Length > 0 ? returns.Average() : 0;
            double stdDev = returns.Length > 1
                ? Math.Sqrt(returns.Sum(r => Math.Pow(r - meanReturn, 2)) / (returns.Length - 1))
                : 0;
            double sharpeRatio = stdDev > 0 ? meanReturn / stdDev * Math.Sqrt(252) : 0;

            // Avg holding days
            double[] holdingDays = closed
                .Where(t => t.ClosedDate.HasValue)
                .Select(t => (t.ClosedDate!.Value - t.Date).TotalDays)
                .ToArray();
            double avgHoldingDays = holdingDays.Length > 0 ? holdingDays.Average() : 0;

            // Long vs Short win rates (Position: 0 = Long, 1 = Short)
            List<TradeCacheDto> longs = [.. closed.Where(t => t.Position == PositionType.Long)];
            List<TradeCacheDto> shorts = [.. closed.Where(t => t.Position == PositionType.Short)];
            double longsWinRate = longs.Count > 0 ? (double)longs.Count(t => t.Pnl > 0) / longs.Count * 100 : 0;
            double shortsWinRate = shorts.Count > 0 ? (double)shorts.Count(t => t.Pnl > 0) / shorts.Count * 100 : 0;

            // Consecutive wins/losses
            int maxConsecWins = 0, maxConsecLosses = 0, curWins = 0, curLosses = 0;
            foreach (TradeCacheDto t in sorted)
            {
                if (t.Pnl > 0) { curWins++; curLosses = 0; maxConsecWins = Math.Max(maxConsecWins, curWins); }
                else { curLosses++; curWins = 0; maxConsecLosses = Math.Max(maxConsecLosses, curLosses); }
            }

            // Avg risk-reward
            List<TradeCacheDto> rrTrades = closed
                .Where(t => t.StopLoss > 0 && t.TargetTier1 > 0 && t.EntryPrice > 0)
                .ToList();
            double[] rrValues = rrTrades
                .Select(t =>
                {
                    double risk = Math.Abs(t.EntryPrice - t.StopLoss);
                    double reward = Math.Abs(t.TargetTier1 - t.EntryPrice);
                    return risk > 0 ? reward / risk : 0;
                })
                .Where(r => r > 0)
                .ToArray();
            double avgRiskReward = rrValues.Length > 0 ? rrValues.Average() : 0;

            return Result<PerformanceSummaryViewModel>.Success(new PerformanceSummaryViewModel(
                Math.Round(totalPnl, 2),
                Math.Round(winRate, 1),
                wins.Count,
                losses.Count,
                closed.Count,
                Math.Round(avgWin, 2),
                Math.Round(avgLoss, 2),
                Math.Round(largestWin, 2),
                Math.Round(largestLoss, 2),
                Math.Round(profitFactor, 2),
                Math.Round(expectancy, 2),
                Math.Round(maxDD, 2),
                Math.Round(maxDDPct, 1),
                Math.Round(sharpeRatio, 2),
                Math.Round(avgHoldingDays, 1),
                Math.Round(longsWinRate, 1),
                Math.Round(shortsWinRate, 1),
                maxConsecWins,
                maxConsecLosses,
                Math.Round(avgRiskReward, 2)));
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/analytics");

            group.MapGet("/performance-summary", async (AnalyticsFilter filter, ISender sender) =>
            {
                Result<PerformanceSummaryViewModel> result = await sender.Send(new Request(filter));

                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result<PerformanceSummaryViewModel>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get performance summary.")
            .WithDescription("Retrieves comprehensive trading performance metrics.")
            .WithTags(Tags.Analytics)
            .RequireAuthorization();
        }
    }
}
