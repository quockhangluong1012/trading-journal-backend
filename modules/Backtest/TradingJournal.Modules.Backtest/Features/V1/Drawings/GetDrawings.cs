namespace TradingJournal.Modules.Backtest.Features.V1.Drawings;

public sealed class GetDrawings
{
    public record Request(int SessionId) : IQuery<Result<string>>
    {
        public int UserId { get; set; }
    }

    internal sealed class Handler(IBacktestDbContext context) : IQueryHandler<Request, Result<string>>
    {
        public async Task<Result<string>> Handle(Request request, CancellationToken cancellationToken)
        {
            bool isOwner = await context.BacktestSessions
                .AnyAsync(s => s.Id == request.SessionId
                               && s.CreatedBy == request.UserId, cancellationToken);

            if (!isOwner)
                return Result<string>.Failure(Error.Create("Session not found."));

            ChartDrawing? drawing = await context.ChartDrawings
                .FirstOrDefaultAsync(d => d.SessionId == request.SessionId, cancellationToken);

            return Result<string>.Success(drawing?.DrawingsJson ?? "[]");
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Drawings);

            group.MapGet("/{sessionId:int}", async (int sessionId, ISender sender) =>
            {
                Result<string> result = await sender.Send(new Request(sessionId));

                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .Produces<Result<string>>(StatusCodes.Status200OK)
            .WithSummary("Get chart drawings for a session.")
            .WithTags(Tags.BacktestDrawings)
            .RequireAuthorization();
        }
    }
}
