namespace TradingJournal.Modules.Goals.Features.V1;

/// <summary>
/// Bulk-applies new <c>SortOrder</c> values to a goal's milestones and tasks.
/// SortOrder is populated on create but otherwise immutable — this is the only
/// way to re-order items.
/// </summary>
public sealed class ReorderItems
{
    public sealed record ReorderEntry(GoalItemType ItemType, int Id, int SortOrder);

    public sealed record Request(
        IReadOnlyList<ReorderEntry> Items,
        int GoalId = 0,
        int UserId = 0) : ICommand<Result>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Items).NotEmpty();
            RuleForEach(x => x.Items).ChildRules(entry =>
            {
                entry.RuleFor(e => e.ItemType)
                    .Must(type => type is GoalItemType.Milestone or GoalItemType.Task)
                    .WithMessage("Only milestones and tasks can be reordered.");
                entry.RuleFor(e => e.Id).GreaterThan(0);
                entry.RuleFor(e => e.SortOrder).GreaterThanOrEqualTo(0);
            });
        }
    }

    public sealed class Handler(IGoalDbContext context) : ICommandHandler<Request, Result>
    {
        public async Task<Result> Handle(Request request, CancellationToken cancellationToken)
        {
            bool ownsGoal = await context.Goals.AnyAsync(
                item => item.Id == request.GoalId && item.CreatedBy == request.UserId,
                cancellationToken);
            if (!ownsGoal)
            {
                return Result.Failure(Error.Create("Goal was not found."));
            }

            HashSet<int> milestoneOrders = request.Items
                .Where(entry => entry.ItemType == GoalItemType.Milestone)
                .Select(entry => entry.Id)
                .ToHashSet();
            HashSet<int> taskOrders = request.Items
                .Where(entry => entry.ItemType == GoalItemType.Task)
                .Select(entry => entry.Id)
                .ToHashSet();

            List<GoalMilestone> milestones = milestoneOrders.Count == 0
                ? []
                : await context.Milestones
                    .Where(item => item.GoalId == request.GoalId
                        && item.CreatedBy == request.UserId
                        && milestoneOrders.Contains(item.Id))
                    .ToListAsync(cancellationToken);

            List<GoalTask> tasks = taskOrders.Count == 0
                ? []
                : await context.GoalTasks
                    .Where(item => item.GoalId == request.GoalId
                        && item.CreatedBy == request.UserId
                        && taskOrders.Contains(item.Id))
                    .ToListAsync(cancellationToken);

            Dictionary<int, int> milestoneTargets = request.Items
                .Where(entry => entry.ItemType == GoalItemType.Milestone)
                .ToDictionary(entry => entry.Id, entry => entry.SortOrder);
            Dictionary<int, int> taskTargets = request.Items
                .Where(entry => entry.ItemType == GoalItemType.Task)
                .ToDictionary(entry => entry.Id, entry => entry.SortOrder);

            foreach (GoalMilestone milestone in milestones)
            {
                milestone.SortOrder = milestoneTargets[milestone.Id];
            }

            foreach (GoalTask task in tasks)
            {
                task.SortOrder = taskTargets[task.Id];
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
            app.MapPatch($"{ApiGroup.V1.Goals}/{{goalId:int}}/reorder", async (
                int goalId,
                [FromBody] Request request,
                ClaimsPrincipal user,
                ISender sender) =>
            {
                Result result = await sender.Send(request with
                {
                    GoalId = goalId,
                    UserId = user.GetCurrentUserId(),
                });
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Reorder a goal's milestones and tasks by assigning new sort orders.")
            .WithTags(Tags.Goals)
            .RequireAuthorization();
        }
    }
}
