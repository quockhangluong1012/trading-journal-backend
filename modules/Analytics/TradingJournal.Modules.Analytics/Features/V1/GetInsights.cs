using TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Modules.Analytics.Features.V1;

public sealed class GetInsights
{
    internal sealed record Request(AnalyticsFilter Filter, int UserId = 0) : IQuery<Result<IReadOnlyCollection<InsightViewModel>>>;

    internal sealed record InsightViewModel(string Type, string Title, string Description);

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

    internal sealed class Handler(ITradeProvider tradeProvider) : IQueryHandler<Request, Result<IReadOnlyCollection<InsightViewModel>>>
    {
        public async Task<Result<IReadOnlyCollection<InsightViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            List<TradeCacheDto> allTrades = await tradeProvider.GetTradesAsync(cancellationToken);
            List<TradeCacheDto> trades = [.. allTrades.Where(t => t.CreatedBy == request.UserId)];
            DateTime fromDate = AnalyticsFilterHelper.GetFromDate(request.Filter);

            List<TradeCacheDto> closed = [.. trades
                .Where(t => t.Status == TradeStatus.Closed && t.Pnl.HasValue)
                .Where(t => fromDate == DateTime.MinValue || (t.ClosedDate.HasValue && t.ClosedDate.Value >= fromDate))];

            if (closed.Count == 0)
            {
                return Result<IReadOnlyCollection<InsightViewModel>>.Success(
                    [new InsightViewModel("info", "Keep trading", "More data will unlock deeper insights. Continue logging trades with complete data for better analysis.")]);
            }

            // Compute metrics inline for insight generation
            List<TradeCacheDto> wins = [.. closed.Where(t => t.Pnl > 0)];
            List<TradeCacheDto> losses = [.. closed.Where(t => t.Pnl <= 0)];

            double winRate = (double)wins.Count / closed.Count * 100;
            double avgWin = wins.Count > 0 ? wins.Average(t => (double)t.Pnl!.Value) : 0;
            double avgLoss = losses.Count > 0 ? Math.Abs(losses.Average(t => (double)t.Pnl!.Value)) : 0;

            double grossProfit = wins.Sum(t => (double)t.Pnl!.Value);
            double grossLoss = Math.Abs(losses.Sum(t => (double)t.Pnl!.Value));
            double profitFactor = grossLoss > 0 ? grossProfit / grossLoss : (grossProfit > 0 ? double.MaxValue : 0);

            // Max drawdown
            List<TradeCacheDto> sorted = [.. closed
                .Where(t => t.ClosedDate.HasValue)
                .OrderBy(t => t.ClosedDate!.Value)];

            double peak = 0, equity = 0, maxDDPct = 0;
            foreach (TradeCacheDto t in sorted)
            {
                equity += (double)t.Pnl!.Value;
                if (equity > peak) peak = equity;
                double dd = peak - equity;
                if (dd > 0 && peak > 0)
                {
                    double ddPct = dd / peak * 100;
                    if (ddPct > maxDDPct) maxDDPct = ddPct;
                }
            }

            // Consecutive
            int maxConsecWins = 0, maxConsecLosses = 0, curWins = 0, curLosses = 0;
            foreach (TradeCacheDto t in sorted)
            {
                if (t.Pnl > 0) { curWins++; curLosses = 0; maxConsecWins = Math.Max(maxConsecWins, curWins); }
                else { curLosses++; curWins = 0; maxConsecLosses = Math.Max(maxConsecLosses, curLosses); }
            }

            // Long vs Short
            List<TradeCacheDto> longs = [.. closed.Where(t => t.Position == PositionType.Long)];
            List<TradeCacheDto> shorts = [.. closed.Where(t => t.Position == PositionType.Short)];
            double longsWinRate = longs.Count > 0 ? (double)longs.Count(t => t.Pnl > 0) / longs.Count * 100 : 0;
            double shortsWinRate = shorts.Count > 0 ? (double)shorts.Count(t => t.Pnl > 0) / shorts.Count * 100 : 0;

            // Avg holding days
            double[] holdingDays = [.. closed
                .Where(t => t.ClosedDate.HasValue)
                .Select(t => (t.ClosedDate!.Value - t.Date).TotalDays)];
            double avgHoldingDays = holdingDays.Length > 0 ? holdingDays.Average() : 0;

            // Avg risk-reward
            double[] rrValues = [.. closed
                .Where(t => t.StopLoss > 0 && t.TargetTier1 > 0 && t.EntryPrice > 0)
                .Select(t =>
                {
                    double risk = Math.Abs(t.EntryPrice - t.StopLoss);
                    double reward = Math.Abs(t.TargetTier1 - t.EntryPrice);
                    return risk > 0 ? reward / risk : 0;
                })
                .Where(r => r > 0)];
            double avgRiskReward = rrValues.Length > 0 ? rrValues.Average() : 0;

            // Sharpe
            double[] returns = [.. sorted.Select(t => (double)t.Pnl!.Value)];
            double meanReturn = returns.Length > 0 ? returns.Average() : 0;
            double stdDev = returns.Length > 1
                ? Math.Sqrt(returns.Sum(r => Math.Pow(r - meanReturn, 2)) / (returns.Length - 1))
                : 0;
            double sharpeRatio = stdDev > 0 ? meanReturn / stdDev * Math.Sqrt(252) : 0;

            // Best/worst asset
            var assetPnl = closed.GroupBy(t => t.Asset)
                .Select(g => new { Asset = g.Key, Pnl = g.Sum(t => (double)t.Pnl!.Value) })
                .ToList();
            var bestAsset = assetPnl.OrderByDescending(a => a.Pnl).FirstOrDefault();
            var worstAsset = assetPnl.OrderBy(a => a.Pnl).FirstOrDefault();

            // Generate insights
            List<InsightViewModel> insights = [];

            // Profitability
            if (profitFactor >= 2)
                insights.Add(new("success", "Strong profit factor", $"Your profit factor of {profitFactor:F2} indicates strong edge. Gross profits significantly outweigh gross losses."));
            else if (profitFactor < 1)
                insights.Add(new("warning", "Profit factor below breakeven", $"A profit factor of {profitFactor:F2} means losses outpace profits. Review your exit strategy and trade selection."));

            // Win rate
            if (winRate >= 60)
                insights.Add(new("success", "High win rate", $"{winRate:F1}% win rate is excellent. Maintain your selection criteria and discipline."));
            else if (winRate < 40)
                insights.Add(new("warning", "Low win rate", $"{winRate:F1}% win rate suggests reviewing entry confluences. Consider adding more confirmation filters."));

            // Risk reward
            if (avgRiskReward >= 2.5)
                insights.Add(new("success", "Great risk-to-reward", $"Average R:R of {avgRiskReward:F1}:1 means you capture large moves relative to risk."));
            else if (avgRiskReward > 0 && avgRiskReward < 1.5)
                insights.Add(new("warning", "Low risk-to-reward", $"Average R:R of {avgRiskReward:F1}:1 requires a very high win rate to stay profitable. Aim for 2:1 or better."));

            // Drawdown
            if (maxDDPct > 20)
                insights.Add(new("warning", "High drawdown", $"Max drawdown of {maxDDPct:F1}% is steep. Consider reducing position sizes or tightening stops."));

            // Consecutive losses
            if (maxConsecLosses >= 3)
                insights.Add(new("warning", "Losing streaks detected", $"You had {maxConsecLosses} consecutive losses. Consider reducing size after 2 consecutive losses."));

            // Position bias
            if (longsWinRate > 0 && shortsWinRate > 0 && Math.Abs(longsWinRate - shortsWinRate) > 25)
            {
                string better = longsWinRate > shortsWinRate ? "Long" : "Short";
                string worse = better == "Long" ? "Short" : "Long";
                insights.Add(new("info", $"Stronger on {better} trades", $"Your {better} win rate is significantly higher. Consider focusing more on {better} setups or reviewing your {worse} strategy."));
            }

            // Holding time
            if (avgHoldingDays > 15)
                insights.Add(new("info", "Long holding periods", $"Average {avgHoldingDays:F0} days per trade. If you're a day/swing trader, exits may need tightening."));

            // Best asset
            if (bestAsset is not null && bestAsset.Pnl > 0)
                insights.Add(new("success", $"Top performer: {bestAsset.Asset}", $"{bestAsset.Asset} generated ${bestAsset.Pnl:N0} in profits. Consider increasing allocation."));

            if (worstAsset is not null && worstAsset.Pnl < 0)
                insights.Add(new("warning", $"Underperformer: {worstAsset.Asset}", $"{worstAsset.Asset} lost ${Math.Abs(worstAsset.Pnl):N0}. Evaluate if this market suits your strategy."));

            // Sharpe
            if (sharpeRatio > 1.5)
                insights.Add(new("success", "Strong risk-adjusted returns", $"Sharpe ratio of {sharpeRatio:F2} indicates good returns relative to volatility."));

            if (insights.Count == 0)
                insights.Add(new("info", "Keep trading", "More data will unlock deeper insights. Continue logging trades with complete data for better analysis."));

            return Result<IReadOnlyCollection<InsightViewModel>>.Success(insights);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/analytics");

            group.MapGet("/insights", async (AnalyticsFilter filter, ISender sender) =>
            {
                var result = await sender.Send(new Request(filter));
                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result<IReadOnlyCollection<InsightViewModel>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get trading insights.")
            .WithDescription("Generates actionable insights and recommendations based on trading data.")
            .WithTags(Tags.Analytics)
            .RequireAuthorization();
        }
    }
}
