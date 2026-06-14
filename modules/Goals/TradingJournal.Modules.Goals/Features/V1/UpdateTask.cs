namespace TradingJournal.Modules.Goals.Features.V1;

public sealed class UpdateTask
{
    public sealed record Request(
        string Title,
        string? Description,
        DateTime? DueDate,
        int? MilestoneId,
        TrackingInput? Tracking,
        int GoalId = 0,
        int TaskId = 0,
        int UserId = 0) : ICommand<Result>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).MaximumLength(2000);
            RuleFor(x => x.MilestoneId).GreaterThan(0).When(x => x.MilestoneId.HasValue);
            RuleFor(x => x.Tracking!).SetValidator(new TrackingInputValidator()).When(x => x.Tracking is not null);
        }
    }

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

            if (request.MilestoneId.HasValue)
            {
                bool milestoneExists = await context.Milestones.AnyAsync(
                    item => item.Id == request.MilestoneId.Value
                        && item.GoalId == request.GoalId
                        && item.CreatedBy == request.UserId,
                    cancellationToken);
                if (!milestoneExists)
                {
                    return Result.Failure(Error.Create("Milestone was not found in this goal."));
                }
            }

            task.Title = request.Title.Trim();
            task.Description = GoalTrackingMapper.Normalize(request.Description);
            task.DueDate = request.DueDate;
            task.MilestoneId = request.MilestoneId;

            if (request.Tracking is not null)
            {
                GoalTrackingMapper.Reconfigure(task, request.Tracking);
            }

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
            app.MapPatch($"{ApiGroup.V1.Goals}/{{goalId:int}}/tasks/{{taskId:int}}", async (
                int goalId,
                int taskId,
                [FromBody] Request request,
                ClaimsPrincipal user,
                ISender sender) =>
            {
                Result result = await sender.Send(request with
                {
                    GoalId = goalId,
                    TaskId = taskId,
                    UserId = user.GetCurrentUserId(),
                });
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Update a task's metadata, parent milestone, and (optionally) its tracking config.")
            .WithTags(Tags.Goals)
            .RequireAuthorization();
        }
    }
}
