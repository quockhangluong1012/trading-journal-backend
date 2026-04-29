namespace TradingJournal.Modules.Trades.Features.V1.Lessons;

public sealed class DeleteLesson
{
    public sealed record Request(int Id, int UserId = 0) : ICommand<Result>;

    public sealed class Handler(ITradeDbContext context) : ICommandHandler<Request, Result>
    {
        public async Task<Result> Handle(Request request, CancellationToken cancellationToken)
        {
            LessonLearned? lesson = await context.LessonsLearned
                .FirstOrDefaultAsync(l => l.Id == request.Id && l.CreatedBy == request.UserId, cancellationToken);

            if (lesson is null)
            {
                return Result.Failure(Error.Create("Lesson not found."));
            }

            // Soft delete via IsDisabled flag (handled by AuditableDbContext)
            lesson.IsDisabled = true;

            await context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Lessons);

            group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal user, ISender sender) =>
            {
                Result result = await sender.Send(new Request(id) with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.NoContent() : Results.NotFound(result);
            })
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Delete a lesson.")
            .WithDescription("Soft-deletes the specified lesson learned entry.")
            .WithTags(Tags.Lessons)
            .RequireAuthorization();
        }
    }
}
