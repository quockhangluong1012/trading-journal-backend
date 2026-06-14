namespace TradingJournal.Modules.Goals.Features.V1;

public sealed class DeleteMilestone
{
    public sealed record Request(int GoalId = 0, int MilestoneId = 0, int UserId = 0) : ICommand<Result>;

    public sealed class Handler(IGoalDbContext context) : ICommandHandler<Request, Result>
    {
        public async Task<Result> Handle(Request request, CancellationToken cancellationToken)
        {
            GoalMilestone? milestone = await context.Milestones.FirstOrDefaultAsync(
                item => item.Id == request.MilestoneId
                    && item.GoalId == request.GoalId
                    && item.CreatedBy == request.UserId,
                cancellationToken);
            if (milestone is null)
            {
                return Result.Failure(Error.Create("Milestone was not found."));
            }

            // Disable the milestone's tasks too; loose tasks on the goal are untouched.
            List<GoalTask> tasks = await context.GoalTasks
                .Where(item => item.MilestoneId == request.MilestoneId)
                .ToListAsync(cancellationToken);

            milestone.IsDisabled = true;
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
            app.MapDelete($"{ApiGroup.V1.Goals}/{{goalId:int}}/milestones/{{milestoneId:int}}", async (
                int goalId,
                int milestoneId,
                ClaimsPrincipal user,
                ISender sender) =>
            {
                Result result = await sender.Send(new Request(goalId, milestoneId, user.GetCurrentUserId()));
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .Produces<Result>()
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Soft-delete a milestone and its tasks.")
            .WithTags(Tags.Goals)
            .RequireAuthorization();
        }
    }
}
