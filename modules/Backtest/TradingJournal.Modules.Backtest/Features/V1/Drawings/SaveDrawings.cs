namespace TradingJournal.Modules.Backtest.Features.V1.Drawings;

public sealed class SaveDrawings
{
    public record Request(int SessionId, string DrawingsJson) : ICommand<Result>
    {
        public int UserId { get; set; }
    }

    internal sealed class Handler(IBacktestDbContext context) : ICommandHandler<Request, Result>
    {
        public async Task<Result> Handle(Request request, CancellationToken cancellationToken)
        {
            bool isOwner = await context.BacktestSessions
                .AnyAsync(s => s.Id == request.SessionId
                               && s.CreatedBy == request.UserId, cancellationToken);

            if (!isOwner)
                return Result.Failure(Error.Create("Session not found."));

            ChartDrawing? existing = await context.ChartDrawings
                .FirstOrDefaultAsync(d => d.SessionId == request.SessionId, cancellationToken);

            if (existing is not null)
            {
                existing.DrawingsJson = request.DrawingsJson;
            }
            else
            {
                await context.ChartDrawings.AddAsync(new ChartDrawing
                {
                    Id = 0,
                    SessionId = request.SessionId,
                    DrawingsJson = request.DrawingsJson
                }, cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Drawings);

            group.MapPut("/{sessionId:int}", async (int sessionId, [FromBody] SaveDrawingsRequest body, ISender sender) =>
            {
                Result result = await sender.Send(new Request(sessionId, body.DrawingsJson));

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Save chart drawings for a session.")
            .WithDescription("Upserts the full drawing JSON array. Frontend auto-saves on every drawing change.")
            .WithTags(Tags.BacktestDrawings)
            .RequireAuthorization();
        }
    }

    public record SaveDrawingsRequest(string DrawingsJson);
}
