using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Modules.RiskManagement.Features.V1;

public sealed class GetRiskDashboard
{
    internal sealed record Request(int UserId = 0) : IQuery<Result<RiskDashboardViewModel>>;

    internal sealed record RiskDashboardViewModel(
        // Config
        decimal AccountBalance,
        decimal DailyLossLimitPercent,
        decimal WeeklyDrawdownCapPercent,
        int MaxOpenPositions,
        // Current state
        decimal DailyPnl,
        decimal DailyPnlPercent,
        decimal WeeklyPnl,
        decimal WeeklyPnlPercent,
        int TodayTradeCount,
        int OpenPositionCount,
        int WeekTradeCount,
        int TodayWins,
        int TodayLosses,
        decimal DailyLimitUsedPercent,
        decimal WeeklyCapUsedPercent,
        bool IsDailyLimitBreached,
        bool IsWeeklyCapBreached,
        // Alerts
        List<RiskAlert> Alerts);

    internal sealed record RiskAlert(string Severity, string Title, string Message);

    internal sealed class Handler(IRiskContextProvider riskContextProvider) : IQueryHandler<Request, Result<RiskDashboardViewModel>>
    {
        public async Task<Result<RiskDashboardViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            RiskAdvisorContextDto context = await riskContextProvider.GetRiskContextAsync(request.UserId, cancellationToken);

            return Result<RiskDashboardViewModel>.Success(ToViewModel(context));
        }

        private static RiskDashboardViewModel ToViewModel(RiskAdvisorContextDto context)
        {
            return new RiskDashboardViewModel(
                context.AccountBalance,
                context.DailyLossLimitPercent,
                context.WeeklyDrawdownCapPercent,
                context.MaxOpenPositions,
                context.DailyPnl,
                context.DailyPnlPercent,
                context.WeeklyPnl,
                context.WeeklyPnlPercent,
                context.TodayTradeCount,
                context.OpenPositionCount,
                context.WeekTradeCount,
                context.TodayWins,
                context.TodayLosses,
                context.DailyLimitUsedPercent,
                context.WeeklyCapUsedPercent,
                context.IsDailyLimitBreached,
                context.IsWeeklyCapBreached,
                [.. context.Alerts.Select(alert => new RiskAlert(alert.Severity, alert.Title, alert.Message))]);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/risk");

            group.MapGet("/dashboard", async (ClaimsPrincipal user, ISender sender) =>
            {
                Result<RiskDashboardViewModel> result = await sender.Send(new Request() with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result<RiskDashboardViewModel>>(StatusCodes.Status200OK)
            .WithSummary("Get risk dashboard data.")
            .WithDescription("Retrieves the current risk state including daily/weekly PnL, limit usage, and alerts.")
            .WithTags(Tags.RiskManagement)
            .RequireAuthorization();
        }
    }
}
