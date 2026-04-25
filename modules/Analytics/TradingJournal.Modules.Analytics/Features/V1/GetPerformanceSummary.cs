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

            AnalyticsMetricsCalculator.AnalyticsMetrics? metrics = AnalyticsMetricsCalculator.Calculate(trades, request.Filter);

            if (metrics is null)
            {
                return Result<PerformanceSummaryViewModel>.Success(new PerformanceSummaryViewModel(
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
            }

            return Result<PerformanceSummaryViewModel>.Success(new PerformanceSummaryViewModel(
                metrics.TotalPnl,
                metrics.WinRate,
                metrics.WinCount,
                metrics.LossCount,
                metrics.TotalClosed,
                metrics.AvgWin,
                metrics.AvgLoss,
                metrics.LargestWin,
                metrics.LargestLoss,
                metrics.ProfitFactor,
                metrics.Expectancy,
                metrics.MaxDrawdown,
                metrics.MaxDrawdownPct,
                metrics.SharpeRatio,
                metrics.AvgHoldingDays,
                metrics.LongsWinRate,
                metrics.ShortsWinRate,
                metrics.ConsecutiveWins,
                metrics.ConsecutiveLosses,
                metrics.AvgRiskReward));
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
