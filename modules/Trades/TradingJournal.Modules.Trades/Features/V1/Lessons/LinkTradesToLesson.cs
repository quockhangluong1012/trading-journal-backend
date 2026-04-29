namespace TradingJournal.Modules.Trades.Features.V1.Lessons;

public sealed class LinkTradesToLesson
{
    public sealed record Request(int LessonId, List<int> TradeIds, int UserId = 0) : ICommand<Result>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.LessonId)
                .GreaterThan(0).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Lesson ID must be greater than 0.");

            RuleFor(x => x.TradeIds)
                .NotEmpty().WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("At least one trade ID must be provided.");
        }
    }

    public sealed class Handler(ITradeDbContext context) : ICommandHandler<Request, Result>
    {
        public async Task<Result> Handle(Request request, CancellationToken cancellationToken)
        {
            bool lessonExists = await context.LessonsLearned
                .AsNoTracking()
                .AnyAsync(l => l.Id == request.LessonId && l.CreatedBy == request.UserId, cancellationToken);

            if (!lessonExists)
                return Result.Failure(Error.Create("Lesson not found."));

            List<int> distinctTradeIds = [.. request.TradeIds.Distinct()];

            int accessibleCount = await context.TradeHistories
                .AsNoTracking()
                .CountAsync(t => distinctTradeIds.Contains(t.Id) && t.CreatedBy == request.UserId, cancellationToken);

            if (accessibleCount != distinctTradeIds.Count)
                return Result.Failure(Error.Create("One or more trade IDs are invalid."));

            List<int> existingLinkedTradeIds = await context.LessonTradeLinks
                .AsNoTracking()
                .Where(ltl => ltl.LessonLearnedId == request.LessonId && distinctTradeIds.Contains(ltl.TradeHistoryId))
                .Select(ltl => ltl.TradeHistoryId)
                .ToListAsync(cancellationToken);

            List<int> newTradeIds = [.. distinctTradeIds.Except(existingLinkedTradeIds)];

            if (newTradeIds.Count == 0)
                return Result.Success();

            await context.LessonTradeLinks.AddRangeAsync(newTradeIds.Select(tradeId => new LessonTradeLink
            {
                Id = 0,
                LessonLearnedId = request.LessonId,
                TradeHistoryId = tradeId
            }), cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Lessons);

            group.MapPost("/{id:int}/trades", async (int id, [FromBody] List<int> tradeIds, ClaimsPrincipal user, ISender sender) =>
            {
                Result result = await sender.Send(new Request(id, tradeIds) with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Link trades to a lesson.")
            .WithDescription("Links trade history entries to a lesson. Duplicates are ignored.")
            .WithTags(Tags.Lessons)
            .RequireAuthorization();
        }
    }
}
