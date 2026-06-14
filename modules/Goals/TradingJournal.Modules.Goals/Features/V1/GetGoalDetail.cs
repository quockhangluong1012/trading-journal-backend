namespace TradingJournal.Modules.Goals.Features.V1;

public sealed class GetGoalDetail
{
    /// <summary>
    /// Auto-tracking can append thousands of progress/activity rows over a goal's
    /// life. The detail endpoint only previews the most recent slice — the full
    /// history is paginated through <see cref="GetGoalHistory"/>.
    /// </summary>
    internal const int HistoryPreviewLimit = 25;

    public sealed record Request(int GoalId, int UserId = 0) : IQuery<Result<GoalDetail>>;

    internal static IQueryable<GoalDetail> BuildQuery(
        IQueryable<Goal> goals,
        int goalId,
        int userId)
    {
        return goals
            .Where(goal => goal.Id == goalId && goal.CreatedBy == userId && !goal.IsDisabled)
            .AsEnumerable()
            .Select(Map)
            .AsQueryable();
    }

    internal static GoalDetail Map(Goal goal) => new(
        goal.Id,
        goal.Title,
        goal.Description,
        goal.StartDate,
        goal.DueDate,
        GoalTrackingMapper.ToSnapshot(goal),
        GoalRollup.ForGoal(goal),
        goal.Milestones
            .OrderBy(milestone => milestone.SortOrder)
            .ThenBy(milestone => milestone.DueDate ?? DateTime.MaxValue)
            .Select(milestone => new GoalMilestoneView(
                milestone.Id,
                milestone.Title,
                milestone.Description,
                milestone.DueDate,
                milestone.SortOrder,
                GoalTrackingMapper.ToSnapshot(milestone),
                GoalRollup.ForMilestone(milestone),
                milestone.Tasks
                    .OrderBy(task => task.SortOrder)
                    .ThenBy(task => task.DueDate ?? DateTime.MaxValue)
                    .Select(GoalTrackingMapper.ToView)
                    .ToList()))
            .ToList(),
        goal.Tasks
            .Where(task => task.MilestoneId == null)
            .OrderBy(task => task.SortOrder)
            .ThenBy(task => task.DueDate ?? DateTime.MaxValue)
            .Select(GoalTrackingMapper.ToView)
            .ToList(),
        goal.ProgressEntries
            .OrderByDescending(entry => entry.CreatedDate)
            .Select(entry => new ProgressEntryView(
                entry.Id,
                entry.ItemType,
                entry.MilestoneId,
                entry.GoalTaskId,
                entry.PreviousValue,
                entry.CurrentValue,
                entry.PreviousIsCompleted,
                entry.CurrentIsCompleted,
                entry.Note,
                entry.CreatedDate))
            .ToList(),
        goal.ActivityLinks
            .OrderByDescending(link => link.RecordedAt)
            .Select(link => new GoalActivityView(
                link.Id,
                link.ItemType,
                link.ItemId,
                link.MetricSource,
                link.SourceType,
                link.SourceId,
                link.Delta,
                link.CompletedItem,
                link.RecordedAt))
            .ToList(),
        goal.CreatedDate,
        goal.UpdatedDate);

    public sealed class Handler(IGoalDbContext context) : IQueryHandler<Request, Result<GoalDetail>>
    {
        public async Task<Result<GoalDetail>> Handle(Request request, CancellationToken cancellationToken)
        {
            Goal? goal = await context.Goals
                .AsNoTracking()
                .Where(item => item.Id == request.GoalId && item.CreatedBy == request.UserId)
                .Include(item => item.Milestones)
                    .ThenInclude(milestone => milestone.Tasks)
                .Include(item => item.Tasks)
                .Include(item => item.ProgressEntries
                    .OrderByDescending(entry => entry.CreatedDate)
                    .Take(HistoryPreviewLimit))
                .Include(item => item.ActivityLinks
                    .OrderByDescending(link => link.RecordedAt)
                    .Take(HistoryPreviewLimit))
                .AsSplitQuery()
                .FirstOrDefaultAsync(cancellationToken);

            // Map through the shared helper the query tests exercise so the production
            // projection and the tested projection stay one and the same.
            return goal is null
                ? Result<GoalDetail>.Failure(Error.Create("Goal was not found."))
                : Result<GoalDetail>.Success(
                    BuildQuery(new[] { goal }.AsQueryable(), request.GoalId, request.UserId).Single());
        }
    }

    [ExcludeFromCodeCoverage]
    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet($"{ApiGroup.V1.Goals}/{{goalId:int}}", async (
                int goalId,
                ClaimsPrincipal user,
                ISender sender) =>
            {
                Result<GoalDetail> result = await sender.Send(
                    new Request(goalId, user.GetCurrentUserId()));
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .Produces<Result<GoalDetail>>()
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Get a goal with milestones, tasks, progress, and history.")
            .WithTags(Tags.Goals)
            .RequireAuthorization();
        }
    }
}
