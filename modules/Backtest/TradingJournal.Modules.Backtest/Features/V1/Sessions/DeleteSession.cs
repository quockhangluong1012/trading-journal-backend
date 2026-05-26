namespace TradingJournal.Modules.Backtest.Features.V1.Sessions;

public sealed class DeleteSession
{
    public record Request(int SessionId) : ICommand<Result>
    {
        public int UserId { get; set; }
    }

    internal sealed class Handler(IBacktestDbContext context) : ICommandHandler<Request, Result>
    {
        public async Task<Result> Handle(Request request, CancellationToken cancellationToken)
        {
            BacktestSession? session = await context.BacktestSessions
                .FirstOrDefaultAsync(s => s.Id == request.SessionId
                                          && s.CreatedBy == request.UserId, cancellationToken);

            if (session is null)
                return Result.Failure(Error.Create("Session not found."));

            // Soft delete
            session.IsDisabled = true;
            await context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Sessions);

            group.MapDelete("/{sessionId:int}", async (int sessionId, ClaimsPrincipal user, ISender sender) =>
            {
                Result result = await sender.Send(new Request(sessionId) with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.NoContent() : Results.NotFound(result);
            })
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Delete a backtest session (soft delete).")
            .WithTags(Tags.BacktestSessions)
            .RequireAuthorization();
        }
    }
}
