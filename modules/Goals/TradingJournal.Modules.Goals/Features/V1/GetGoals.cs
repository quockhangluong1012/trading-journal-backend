namespace TradingJournal.Modules.Goals.Features.V1;

public sealed class GetGoals
{
    public sealed record Request(bool IncludeCompleted = false, int UserId = 0)
        : IQuery<Result<IReadOnlyList<GoalSummary>>>;

    internal static IQueryable<GoalSummary> BuildQuery(
        IQueryable<Goal> goals,
        int userId,
        bool includeCompleted)
    {
        return goals
            .Where(goal => goal.CreatedBy == userId
                && !goal.IsDisabled
                && (includeCompleted || !goal.IsCompleted))
            .AsEnumerable()
            .OrderBy(goal => goal.IsCompleted)
            .ThenBy(goal => goal.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(goal => goal.UpdatedDate ?? goal.CreatedDate)
            .Select(Map)
            .AsQueryable();
    }

    internal static GoalSummary Map(Goal goal) => new(
        goal.Id,
        goal.Title,
        goal.Description,
        goal.StartDate,
        goal.DueDate,
        GoalTrackingMapper.ToSnapshot(goal),
        GoalRollup.ForGoal(goal),
        goal.Milestones.Count,
        goal.Tasks.Count,
        goal.Tasks.Count(task => task.IsCompleted),
        goal.CreatedDate,
        goal.UpdatedDate);

    public sealed class Handler(IGoalDbContext context)
        : IQueryHandler<Request, Result<IReadOnlyList<GoalSummary>>>
    {
        public async Task<Result<IReadOnlyList<GoalSummary>>> Handle(
            Request request,
            CancellationToken cancellationToken)
        {
            if (request.UserId <= 0)
            {
                return Result<IReadOnlyList<GoalSummary>>.Failure(Error.Create("Current user is required."));
            }

            List<Goal> goals = await context.Goals
                .AsNoTracking()
                .Where(goal => goal.CreatedBy == request.UserId
                    && (request.IncludeCompleted || !goal.IsCompleted))
                .Include(goal => goal.Milestones)
                .Include(goal => goal.Tasks)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            // Reuse the same filtering/ordering/mapping the query tests assert against so
            // the two can never silently diverge.
            IReadOnlyList<GoalSummary> result = BuildQuery(
                goals.AsQueryable(), request.UserId, request.IncludeCompleted).ToList();

            return Result<IReadOnlyList<GoalSummary>>.Success(result);
        }
    }

    [ExcludeFromCodeCoverage]
    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet(ApiGroup.V1.Goals, async (
                [FromQuery] bool includeCompleted,
                ClaimsPrincipal user,
                ISender sender) =>
            {
                Result<IReadOnlyList<GoalSummary>> result = await sender.Send(
                    new Request(includeCompleted, user.GetCurrentUserId()));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<IReadOnlyList<GoalSummary>>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get the current user's goals and progress summaries.")
            .WithTags(Tags.Goals)
            .RequireAuthorization();
        }
    }
}
