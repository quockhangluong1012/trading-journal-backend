namespace TradingJournal.Modules.Goals.Features.V1;

public sealed class DeleteGoal
{
    public sealed record Request(int GoalId = 0, int UserId = 0) : ICommand<Result>;

    public sealed class Handler(IGoalDbContext context) : ICommandHandler<Request, Result>
    {
        public async Task<Result> Handle(Request request, CancellationToken cancellationToken)
        {
            Goal? goal = await context.Goals.FirstOrDefaultAsync(
                item => item.Id == request.GoalId && item.CreatedBy == request.UserId,
                cancellationToken);
            if (goal is null)
            {
                return Result.Failure(Error.Create("Goal was not found."));
            }

            // Soft-delete the whole subtree so the goal's milestones/tasks stop
            // matching auto-tracking queries (those filter on the item's own flag,
            // not the parent's).
            List<GoalMilestone> milestones = await context.Milestones
                .Where(item => item.GoalId == request.GoalId)
                .ToListAsync(cancellationToken);
            List<GoalTask> tasks = await context.GoalTasks
                .Where(item => item.GoalId == request.GoalId)
                .ToListAsync(cancellationToken);

            goal.IsDisabled = true;
            milestones.ForEach(item => item.IsDisabled = true);
            tasks.ForEach(item => item.IsDisabled = true);

            return await context.ExecuteInTransactionAsync(async ct =>
            {
                await context.SaveChangesAsync(ct);
                return Result.Success();
            }, cancellationToken);
        }
    }

    [ExcludeFromCodeCoverage]
    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapDelete($"{ApiGroup.V1.Goals}/{{goalId:int}}", async (
                int goalId,
                ClaimsPrincipal user,
                ISender sender) =>
            {
                Result result = await sender.Send(new Request(goalId, user.GetCurrentUserId()));
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .Produces<Result>()
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Soft-delete a goal and all of its milestones and tasks.")
            .WithTags(Tags.Goals)
            .RequireAuthorization();
        }
    }
}
