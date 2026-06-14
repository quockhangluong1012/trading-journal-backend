namespace TradingJournal.Modules.Goals.Features.V1;

/// <summary>
/// Lightweight headline counts for the goals list intro. Computed independently
/// of the list query so "Completed" is accurate even when completed goals are
/// filtered out of the visible list.
/// </summary>
public sealed class GetGoalStats
{
    public sealed record Request(int UserId = 0) : IQuery<Result<GoalStats>>;

    internal static GoalStats Build(IReadOnlyCollection<Goal> goals)
    {
        if (goals.Count == 0)
        {
            return new GoalStats(0, 0, 0m);
        }

        int completed = goals.Count(goal => goal.IsCompleted);
        decimal averageProgress = decimal.Round(goals.Average(GoalRollup.ForGoal), 2);

        return new GoalStats(goals.Count - completed, completed, averageProgress);
    }

    public sealed class Handler(IGoalDbContext context) : IQueryHandler<Request, Result<GoalStats>>
    {
        public async Task<Result<GoalStats>> Handle(Request request, CancellationToken cancellationToken)
        {
            if (request.UserId <= 0)
            {
                return Result<GoalStats>.Failure(Error.Create("Current user is required."));
            }

            List<Goal> goals = await context.Goals
                .AsNoTracking()
                .Where(goal => goal.CreatedBy == request.UserId)
                .Include(goal => goal.Milestones)
                .Include(goal => goal.Tasks)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            return Result<GoalStats>.Success(Build(goals));
        }
    }

    [ExcludeFromCodeCoverage]
    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet($"{ApiGroup.V1.Goals}/stats", async (
                ClaimsPrincipal user,
                ISender sender) =>
            {
                Result<GoalStats> result = await sender.Send(new Request(user.GetCurrentUserId()));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<GoalStats>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get active/completed counts and average progress for the current user's goals.")
            .WithTags(Tags.Goals)
            .RequireAuthorization();
        }
    }
}
