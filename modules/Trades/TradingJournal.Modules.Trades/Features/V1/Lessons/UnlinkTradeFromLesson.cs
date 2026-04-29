namespace TradingJournal.Modules.Trades.Features.V1.Lessons;

public sealed class UnlinkTradeFromLesson
{
    public sealed record Request(int LessonId, int TradeId, int UserId = 0) : ICommand<Result>;

    public sealed class Handler(ITradeDbContext context) : ICommandHandler<Request, Result>
    {
        public async Task<Result> Handle(Request request, CancellationToken cancellationToken)
        {
            LessonTradeLink? link = await context.LessonTradeLinks
                .Include(ltl => ltl.LessonLearned)
                .FirstOrDefaultAsync(ltl =>
                    ltl.LessonLearnedId == request.LessonId &&
                    ltl.TradeHistoryId == request.TradeId &&
                    ltl.LessonLearned.CreatedBy == request.UserId, cancellationToken);

            if (link is null)
                return Result.Failure(Error.Create("Link not found."));

            link.IsDisabled = true;
            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Lessons);

            group.MapDelete("/{id:int}/trades/{tradeId:int}", async (int id, int tradeId, ClaimsPrincipal user, ISender sender) =>
            {
                Result result = await sender.Send(new Request(id, tradeId) with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess ? Results.NoContent() : Results.NotFound(result);
            })
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Unlink a trade from a lesson.")
            .WithTags(Tags.Lessons)
            .RequireAuthorization();
        }
    }
}
