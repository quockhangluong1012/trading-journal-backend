namespace TradingJournal.Modules.Goals.Features.V1;

public sealed class DeleteTask
{
    public sealed record Request(int GoalId = 0, int TaskId = 0, int UserId = 0) : ICommand<Result>;

    public sealed class Handler(IGoalDbContext context) : ICommandHandler<Request, Result>
    {
        public async Task<Result> Handle(Request request, CancellationToken cancellationToken)
        {
            GoalTask? task = await context.GoalTasks.FirstOrDefaultAsync(
                item => item.Id == request.TaskId
                    && item.GoalId == request.GoalId
                    && item.CreatedBy == request.UserId,
                cancellationToken);
            if (task is null)
            {
                return Result.Failure(Error.Create("Task was not found."));
            }

            task.IsDisabled = true;

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
            app.MapDelete($"{ApiGroup.V1.Goals}/{{goalId:int}}/tasks/{{taskId:int}}", async (
                int goalId,
                int taskId,
                ClaimsPrincipal user,
                ISender sender) =>
            {
                Result result = await sender.Send(new Request(goalId, taskId, user.GetCurrentUserId()));
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .Produces<Result>()
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Soft-delete a task.")
            .WithTags(Tags.Goals)
            .RequireAuthorization();
        }
    }
}
