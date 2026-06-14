namespace TradingJournal.Modules.Goals.Features.V1;

using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Messaging.Shared.Contracts;

public sealed class UpdateProgress
{
    public sealed record GoalRequest(
        int GoalId,
        decimal? Value,
        bool? IsCompleted,
        string? Note,
        int UserId = 0) : ICommand<Result<ProgressResult>>;

    public sealed record MilestoneRequest(
        int GoalId,
        int MilestoneId,
        decimal? Value,
        bool? IsCompleted,
        string? Note,
        int UserId = 0) : ICommand<Result<ProgressResult>>;

    public sealed record TaskRequest(
        int GoalId,
        int TaskId,
        decimal? Value,
        bool? IsCompleted,
        string? Note,
        int UserId = 0) : ICommand<Result<ProgressResult>>;

    public sealed class GoalHandler(IGoalDbContext context, IEventBus eventBus) : ICommandHandler<GoalRequest, Result<ProgressResult>>
    {
        public async Task<Result<ProgressResult>> Handle(GoalRequest request, CancellationToken cancellationToken)
        {
            Goal? goal = await context.Goals.FirstOrDefaultAsync(
                item => item.Id == request.GoalId && item.CreatedBy == request.UserId,
                cancellationToken);
            if (goal is null)
            {
                return Result<ProgressResult>.Failure(Error.Create("Goal was not found."));
            }

            return await ApplyAndSave(
                context,
                eventBus,
                goal,
                goal.Id,
                goal.Title,
                goal.Id,
                GoalItemType.Goal,
                null,
                null,
                request.Value,
                request.IsCompleted,
                request.Note,
                request.UserId,
                cancellationToken);
        }
    }

    public sealed class MilestoneHandler(IGoalDbContext context, IEventBus eventBus) : ICommandHandler<MilestoneRequest, Result<ProgressResult>>
    {
        public async Task<Result<ProgressResult>> Handle(MilestoneRequest request, CancellationToken cancellationToken)
        {
            GoalMilestone? milestone = await context.Milestones.FirstOrDefaultAsync(
                item => item.Id == request.MilestoneId
                    && item.GoalId == request.GoalId
                    && item.CreatedBy == request.UserId,
                cancellationToken);
            if (milestone is null)
            {
                return Result<ProgressResult>.Failure(Error.Create("Milestone was not found."));
            }

            return await ApplyAndSave(
                context,
                eventBus,
                milestone,
                milestone.Id,
                milestone.Title,
                milestone.GoalId,
                GoalItemType.Milestone,
                milestone.Id,
                null,
                request.Value,
                request.IsCompleted,
                request.Note,
                request.UserId,
                cancellationToken);
        }
    }

    public sealed class TaskHandler(IGoalDbContext context, IEventBus eventBus) : ICommandHandler<TaskRequest, Result<ProgressResult>>
    {
        public async Task<Result<ProgressResult>> Handle(TaskRequest request, CancellationToken cancellationToken)
        {
            GoalTask? task = await context.GoalTasks.FirstOrDefaultAsync(
                item => item.Id == request.TaskId
                    && item.GoalId == request.GoalId
                    && item.CreatedBy == request.UserId,
                cancellationToken);
            if (task is null)
            {
                return Result<ProgressResult>.Failure(Error.Create("Task was not found."));
            }

            return await ApplyAndSave(
                context,
                eventBus,
                task,
                task.Id,
                task.Title,
                task.GoalId,
                GoalItemType.Task,
                task.MilestoneId,
                task.Id,
                request.Value,
                request.IsCompleted,
                request.Note,
                request.UserId,
                cancellationToken);
        }
    }

    private static async Task<Result<ProgressResult>> ApplyAndSave(
        IGoalDbContext context,
        IEventBus eventBus,
        ITrackableGoalItem item,
        int itemId,
        string title,
        int goalId,
        GoalItemType itemType,
        int? milestoneId,
        int? taskId,
        decimal? value,
        bool? isCompleted,
        string? note,
        int userId,
        CancellationToken cancellationToken)
    {
        decimal? previousValue = item.CurrentValue;
        bool previousIsCompleted = item.IsCompleted;

        if (item.TrackingMode == TrackingMode.Manual)
        {
            if (value.HasValue || !isCompleted.HasValue)
            {
                return Result<ProgressResult>.Failure(
                    Error.Create("Manual tracking requires isCompleted and does not accept a metric value."));
            }

            item.IsCompleted = isCompleted.Value;
        }
        else
        {
            if (item.MetricSource.HasValue)
            {
                return Result<ProgressResult>.Failure(
                    Error.Create("Automatically tracked metrics cannot be updated manually."));
            }

            if (!value.HasValue || isCompleted.HasValue || item.MetricDirection is null || item.TargetValue is null)
            {
                return Result<ProgressResult>.Failure(
                    Error.Create("Metric tracking requires a value and derives completion automatically."));
            }

            item.CurrentValue = value.Value;
            item.IsCompleted = TrackingProgress.IsMetricComplete(
                item.MetricDirection.Value,
                value.Value,
                item.TargetValue.Value);
        }

        DateTime completedAt = DateTime.UtcNow;
        item.CompletedDate = item.IsCompleted ? completedAt : null;

        // Reward/notify only on the very first completion. Manual items can be
        // toggled off and on again — without this guard each re-completion would
        // re-publish GoalItemCompletedEvent and farm karma indefinitely.
        bool firstCompletion = !previousIsCompleted && item.IsCompleted && item.FirstCompletedDate is null;
        if (firstCompletion)
        {
            item.FirstCompletedDate = completedAt;
        }

        context.ProgressEntries.Add(new GoalProgressEntry
        {
            GoalId = goalId,
            ItemType = itemType,
            MilestoneId = milestoneId,
            GoalTaskId = taskId,
            PreviousValue = previousValue,
            CurrentValue = item.CurrentValue,
            PreviousIsCompleted = previousIsCompleted,
            CurrentIsCompleted = item.IsCompleted,
            Note = GoalTrackingMapper.Normalize(note),
            CreatedBy = userId,
        });

        return await context.ExecuteInTransactionAsync(async ct =>
        {
            int rows = await context.SaveChangesAsync(ct);
            if (rows <= 0)
            {
                return Result<ProgressResult>.Failure(Error.Create("Failed to update progress."));
            }

            if (firstCompletion)
            {
                await eventBus.PublishAsync(new GoalItemCompletedEvent(
                    Guid.NewGuid(),
                    userId,
                    goalId,
                    itemId,
                    itemType switch
                    {
                        GoalItemType.Goal => GoalItemKind.Goal,
                        GoalItemType.Milestone => GoalItemKind.Milestone,
                        GoalItemType.Task => GoalItemKind.Task,
                        _ => throw new ArgumentOutOfRangeException(nameof(itemType), itemType, null),
                    },
                    title,
                    item.CompletedDate ?? completedAt), ct);
            }

            decimal progress = TrackingProgress.Calculate(
                item.TrackingMode,
                item.MetricDirection,
                item.StartValue,
                item.CurrentValue,
                item.TargetValue,
                item.IsCompleted);

            return Result<ProgressResult>.Success(new ProgressResult(
                item.CurrentValue,
                progress,
                item.IsCompleted,
                item.CompletedDate));
        }, cancellationToken);
    }

    [ExcludeFromCodeCoverage]
    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPatch($"{ApiGroup.V1.Goals}/{{goalId:int}}/progress", async (
                int goalId,
                [FromBody] GoalRequest request,
                ClaimsPrincipal user,
                ISender sender) => ToHttpResult(await sender.Send(request with
                {
                    GoalId = goalId,
                    UserId = user.GetCurrentUserId(),
                })))
                .WithSummary("Update manual or metric progress for a goal.")
                .WithTags(Tags.Goals)
                .RequireAuthorization();

            app.MapPatch($"{ApiGroup.V1.Goals}/{{goalId:int}}/milestones/{{milestoneId:int}}/progress", async (
                int goalId,
                int milestoneId,
                [FromBody] MilestoneRequest request,
                ClaimsPrincipal user,
                ISender sender) => ToHttpResult(await sender.Send(request with
                {
                    GoalId = goalId,
                    MilestoneId = milestoneId,
                    UserId = user.GetCurrentUserId(),
                })))
                .WithSummary("Update manual or metric progress for a milestone.")
                .WithTags(Tags.Goals)
                .RequireAuthorization();

            app.MapPatch($"{ApiGroup.V1.Goals}/{{goalId:int}}/tasks/{{taskId:int}}/progress", async (
                int goalId,
                int taskId,
                [FromBody] TaskRequest request,
                ClaimsPrincipal user,
                ISender sender) => ToHttpResult(await sender.Send(request with
                {
                    GoalId = goalId,
                    TaskId = taskId,
                    UserId = user.GetCurrentUserId(),
                })))
                .WithSummary("Update manual or metric progress for a task.")
                .WithTags(Tags.Goals)
                .RequireAuthorization();
        }

        private static IResult ToHttpResult(Result<ProgressResult> result) =>
            result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
    }
}
