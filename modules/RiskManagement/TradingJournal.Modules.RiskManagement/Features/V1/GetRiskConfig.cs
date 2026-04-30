using TradingJournal.Modules.RiskManagement.Common.Helpers;

namespace TradingJournal.Modules.RiskManagement.Features.V1;

public sealed class GetRiskConfig
{
    internal sealed record Request(int UserId = 0) : IQuery<Result<RiskConfigViewModel>>;

    internal sealed record RiskConfigViewModel(
        decimal DailyLossLimitPercent,
        decimal WeeklyDrawdownCapPercent,
        decimal RiskPerTradePercent,
        int MaxOpenPositions,
        int MaxCorrelatedPositions,
        decimal AccountBalance);

    internal sealed class Handler(IRiskDbContext context) : IQueryHandler<Request, Result<RiskConfigViewModel>>
    {
        public async Task<Result<RiskConfigViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            RiskConfig? config = await context.RiskConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CreatedBy == request.UserId, cancellationToken);

            if (config is null)
            {
                // Return sensible defaults if no config exists yet
                return Result<RiskConfigViewModel>.Success(new RiskConfigViewModel(
                    2.0m, 5.0m, 1.0m, 5, 3, 10000m));
            }

            return Result<RiskConfigViewModel>.Success(new RiskConfigViewModel(
                config.DailyLossLimitPercent,
                config.WeeklyDrawdownCapPercent,
                config.RiskPerTradePercent,
                config.MaxOpenPositions,
                config.MaxCorrelatedPositions,
                config.AccountBalance));
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/risk");

            group.MapGet("/config", async (ClaimsPrincipal user, ISender sender) =>
            {
                Result<RiskConfigViewModel> result = await sender.Send(new Request() with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result<RiskConfigViewModel>>(StatusCodes.Status200OK)
            .WithSummary("Get risk configuration.")
            .WithDescription("Retrieves the user's risk management settings.")
            .WithTags(Tags.RiskManagement)
            .RequireAuthorization();
        }
    }
}
