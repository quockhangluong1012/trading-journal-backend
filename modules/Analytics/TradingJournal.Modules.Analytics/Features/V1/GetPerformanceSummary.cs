using TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Modules.Analytics.Features.V1;

public sealed class GetPerformanceSummary
{
    internal sealed record Request(AnalyticsFilter Filter, int UserId = 0) : IQuery<Result<PerformanceSummaryViewModel>>;

    internal sealed record PerformanceSummaryViewModel(
        decimal TotalPnl,
        decimal WinRate,
        int Wins,
        int Losses,
        int TotalClosed,
        decimal avgWin,
        decimal avgLoss,
        decimal largestWin,
        decimal largestLoss,
        decimal ProfitFactor,
        decimal Expectancy,
        decimal MaxDrawdown,
        decimal MaxDrawdownPct,
        decimal SharpeRatio,
        decimal AvgHoldingDays,
        decimal LongsWinRate,
        decimal ShortsWinRate,
        int ConsecutiveWins,
        int ConsecutiveLosses,
        decimal AvgRiskReward);

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
            List<TradeCacheDto> trades = await tradeProvider.GetTradesAsync(request.UserId, cancellationToken);
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

            decimal totalPnl = closed.Sum(t => (decimal)t.Pnl!.Value);
            decimal winRate = (decimal)wins.Count / closed.Count * 100;
            decimal avgWin = wins.Count > 0 ? wins.Average(t => (decimal)t.Pnl!.Value) : 0;
            decimal avgLoss = losses.Count > 0 ? Math.Abs(losses.Average(t => (decimal)t.Pnl!.Value)) : 0;
            decimal largestWin = wins.Count > 0 ? (decimal)wins.Max(t => t.Pnl!.Value) : 0;
            decimal largestLoss = losses.Count > 0 ? (decimal)losses.Min(t => t.Pnl!.Value) : 0;

            // Profit factor
            decimal grossProfit = wins.Sum(t => (decimal)t.Pnl!.Value);
            decimal grossLoss = Math.Abs(losses.Sum(t => (decimal)t.Pnl!.Value));
            decimal profitFactor = grossLoss > 0 ? grossProfit / grossLoss : (grossProfit > 0 ? decimal.MaxValue : 0);

            // Expectancy
            decimal expectancy = (winRate / 100 * avgWin) - ((1 - winRate / 100) * avgLoss);

            // Max drawdown
            List<TradeCacheDto> sorted = closed
                .Where(t => t.ClosedDate.HasValue)
                .OrderBy(t => t.ClosedDate!.Value)
                .ToList();

            decimal peak = 0, equity = 0, maxDD = 0, maxDDPct = 0;
            foreach (TradeCacheDto t in sorted)
            {
                equity += (decimal)t.Pnl!.Value;
                if (equity > peak) peak = equity;
                decimal dd = peak - equity;
                if (dd > maxDD)
                {
                    maxDD = dd;
                    maxDDPct = peak > 0 ? dd / peak * 100 : 0;
                }
            }

            // Sharpe ratio (simplified)
            double[] returns = sorted.Select(t => (double)t.Pnl!.Value).ToArray();
            decimal meanReturn = returns.Length > 0 ? (decimal)returns.Average() : 0;
            decimal stdDev = returns.Length > 1
                ? (decimal)Math.Sqrt(returns.Sum(r => Math.Pow(r - (double)meanReturn, 2)) / (returns.Length - 1))
                : 0;
            decimal sharpeRatio = stdDev > 0 ? meanReturn / stdDev * (decimal)Math.Sqrt(252) : 0;

            // Avg holding days
            double[] holdingDays = closed
                .Where(t => t.ClosedDate.HasValue)
                .Select(t => (t.ClosedDate!.Value - t.Date).TotalDays)
                .ToArray();
            decimal avgHoldingDays = holdingDays.Length > 0 ? (decimal)holdingDays.Average() : 0;

            // Long vs Short win rates (Position: 0 = Long, 1 = Short)
            List<TradeCacheDto> longs = [.. closed.Where(t => t.Position == PositionType.Long)];
            List<TradeCacheDto> shorts = [.. closed.Where(t => t.Position == PositionType.Short)];
            decimal longsWinRate = longs.Count > 0 ? (decimal)longs.Count(t => t.Pnl > 0) / longs.Count * 100 : 0;
            decimal shortsWinRate = shorts.Count > 0 ? (decimal)shorts.Count(t => t.Pnl > 0) / shorts.Count * 100 : 0;

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
                    decimal risk = Math.Abs(t.EntryPrice - t.StopLoss);
                    decimal reward = Math.Abs(t.TargetTier1 - t.EntryPrice);
                    return risk > 0 ? (double)(reward / risk) : 0;
                })
                .Where(r => r > 0)
                .ToArray();
            decimal avgRiskReward = rrValues.Length > 0 ? (decimal)rrValues.Average() : 0;

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

            group.MapGet("/performance-summary", async (AnalyticsFilter filter, ClaimsPrincipal user, ISender sender) =>
            {
                Result<PerformanceSummaryViewModel> result = await sender.Send(new Request(filter) with { UserId = user.GetCurrentUserId() });

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
