using TradingJournal.Modules.Backtest.Dto;

namespace TradingJournal.Modules.Backtest.Features.V1.Sessions;

public sealed class GetSessionDetail
{
    public record Request(int SessionId) : IQuery<Result<SessionDetailDto>>
    {
        public int UserId { get; set; }
    }

    internal sealed class Handler(IBacktestDbContext context) : IQueryHandler<Request, Result<SessionDetailDto>>
    {
        public async Task<Result<SessionDetailDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            SessionDetailDto? session = await context.BacktestSessions
                .Where(s => s.Id == request.SessionId && s.CreatedBy == request.UserId && !s.IsDisabled)
                .Select(s => new SessionDetailDto(
                    s.Id,
                    s.Asset,
                    s.StartDate,
                    s.EndDate,
                    s.InitialBalance,
                    s.CurrentBalance,
                    s.InitialBalance > 0
                        ? Math.Round((s.CurrentBalance - s.InitialBalance) / s.InitialBalance * 100m, 2)
                        : 0m,
                    s.Status.ToString(),
                    s.CurrentTimestamp,
                    s.ActiveTimeframe.ToString(),
                    s.PlaybackSpeed,
                    s.Leverage,
                    s.MaintenanceMarginPercentage,
                    s.IsDataReady,
                    s.Orders.Count,
                    s.Orders.Count(o => o.Status == BacktestOrderStatus.Active),
                    s.TradeResults.Count,
                    s.CreatedDate))
                .FirstOrDefaultAsync(cancellationToken);

            if (session is null)
                return Result<SessionDetailDto>.Failure(Error.Create("Session not found."));

            return Result<SessionDetailDto>.Success(session);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Sessions);

            group.MapGet("/{sessionId:int}", async (int sessionId, ClaimsPrincipal user, ISender sender) =>
            {
                Result<SessionDetailDto> result = await sender.Send(new Request(sessionId) with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .Produces<Result<SessionDetailDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Get backtest session details.")
            .WithTags(Tags.BacktestSessions)
            .RequireAuthorization();
        }
    }
}
