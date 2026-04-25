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
            List<TradeCacheDto> trades = await tradeProvider.GetTradesAsync(request.UserId, cancellationToken);

            AnalyticsMetricsCalculator.AnalyticsMetrics? metrics = AnalyticsMetricsCalculator.Calculate(trades, request.Filter);

            if (metrics is null)
            {
                return Result<IReadOnlyCollection<InsightViewModel>>.Success(
                    [new InsightViewModel("info", "Keep trading", "More data will unlock deeper insights. Continue logging trades with complete data for better analysis.")]);
            }

            // Generate insights from pre-computed metrics
            List<InsightViewModel> insights = [];

            // Profitability
            if (metrics.ProfitFactor >= 2)
                insights.Add(new("success", "Strong profit factor", $"Your profit factor of {metrics.ProfitFactor:F2} indicates strong edge. Gross profits significantly outweigh gross losses."));
            else if (metrics.ProfitFactor < 1)
                insights.Add(new("warning", "Profit factor below breakeven", $"A profit factor of {metrics.ProfitFactor:F2} means losses outpace profits. Review your exit strategy and trade selection."));

            // Win rate
            if (metrics.WinRate >= 60)
                insights.Add(new("success", "High win rate", $"{metrics.WinRate:F1}% win rate is excellent. Maintain your selection criteria and discipline."));
            else if (metrics.WinRate < 40)
                insights.Add(new("warning", "Low win rate", $"{metrics.WinRate:F1}% win rate suggests reviewing entry confluences. Consider adding more confirmation filters."));

            // Risk reward
            if (metrics.AvgRiskReward >= 2.5m)
                insights.Add(new("success", "Great risk-to-reward", $"Average R:R of {metrics.AvgRiskReward:F1}:1 means you capture large moves relative to risk."));
            else if (metrics.AvgRiskReward > 0 && metrics.AvgRiskReward < 1.5m)
                insights.Add(new("warning", "Low risk-to-reward", $"Average R:R of {metrics.AvgRiskReward:F1}:1 requires a very high win rate to stay profitable. Aim for 2:1 or better."));

            // Drawdown
            if (metrics.MaxDrawdownPct > 20)
                insights.Add(new("warning", "High drawdown", $"Max drawdown of {metrics.MaxDrawdownPct:F1}% is steep. Consider reducing position sizes or tightening stops."));

            // Consecutive losses
            if (metrics.ConsecutiveLosses >= 3)
                insights.Add(new("warning", "Losing streaks detected", $"You had {metrics.ConsecutiveLosses} consecutive losses. Consider reducing size after 2 consecutive losses."));

            // Position bias
            if (metrics.LongsWinRate > 0 && metrics.ShortsWinRate > 0 && Math.Abs(metrics.LongsWinRate - metrics.ShortsWinRate) > 25)
            {
                string better = metrics.LongsWinRate > metrics.ShortsWinRate ? "Long" : "Short";
                string worse = better == "Long" ? "Short" : "Long";
                insights.Add(new("info", $"Stronger on {better} trades", $"Your {better} win rate is significantly higher. Consider focusing more on {better} setups or reviewing your {worse} strategy."));
            }

            // Holding time
            if (metrics.AvgHoldingDays > 15)
                insights.Add(new("info", "Long holding periods", $"Average {metrics.AvgHoldingDays:F0} days per trade. If you're a day/swing trader, exits may need tightening."));

            // Best asset
            if (metrics.BestAsset is not null && metrics.BestAsset.Pnl > 0)
                insights.Add(new("success", $"Top performer: {metrics.BestAsset.Asset}", $"{metrics.BestAsset.Asset} generated ${metrics.BestAsset.Pnl:N0} in profits. Consider increasing allocation."));

            if (metrics.WorstAsset is not null && metrics.WorstAsset.Pnl < 0)
                insights.Add(new("warning", $"Underperformer: {metrics.WorstAsset.Asset}", $"{metrics.WorstAsset.Asset} lost ${Math.Abs(metrics.WorstAsset.Pnl):N0}. Evaluate if this market suits your strategy."));

            // Sharpe
            if (metrics.SharpeRatio > 1.5m)
                insights.Add(new("success", "Strong risk-adjusted returns", $"Sharpe ratio of {metrics.SharpeRatio:F2} indicates good returns relative to volatility."));

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

            group.MapGet("/insights", async (AnalyticsFilter filter, ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request(filter) with { UserId = user.GetCurrentUserId() });
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
