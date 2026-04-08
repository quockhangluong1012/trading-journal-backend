using TradingJournal.Modules.Backtest.Dto;
using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Backtest.Features.V1.Sessions;

public sealed class GetSessions
{
    public record Request() : IQuery<Result<List<SessionListDto>>>
    {
        public int UserId { get; set; }
    }

    internal sealed class Handler(IBacktestDbContext context) : IQueryHandler<Request, Result<List<SessionListDto>>>
    {
        public async Task<Result<List<SessionListDto>>> Handle(Request request, CancellationToken cancellationToken)
        {
            List<SessionListDto> sessions = await context.BacktestSessions
                .Where(s => s.CreatedBy == request.UserId && !s.IsDisabled)
                .OrderByDescending(s => s.CreatedDate)
                .Select(s => new SessionListDto(
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
                    s.IsDataReady,
                    s.CreatedDate))
                .ToListAsync(cancellationToken);

            return Result<List<SessionListDto>>.Success(sessions);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Sessions);

            group.MapGet("/", async (ISender sender) =>
            {
                Result<List<SessionListDto>> result = await sender.Send(new Request());

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<List<SessionListDto>>>(StatusCodes.Status200OK)
            .WithSummary("Get all backtest sessions for the current user.")
            .WithTags(Tags.BacktestSessions)
            .RequireAuthorization();
        }
    }
}
