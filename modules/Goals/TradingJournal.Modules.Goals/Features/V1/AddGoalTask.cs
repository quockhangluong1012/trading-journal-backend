namespace TradingJournal.Modules.Goals.Features.V1;

public sealed class AddGoalTask
{
    public sealed record Request(
        int GoalId,
        int? MilestoneId,
        string Title,
        string? Description,
        DateTime? DueDate,
        int SortOrder,
        TrackingInput Tracking,
        int UserId = 0) : ICommand<Result<int>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.GoalId).GreaterThan(0);
            RuleFor(x => x.MilestoneId).GreaterThan(0).When(x => x.MilestoneId.HasValue);
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).MaximumLength(2000);
            RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Tracking).NotNull().SetValidator(new TrackingInputValidator());
        }
    }

    public sealed class Handler(IGoalDbContext context) : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            Goal? goal = await context.Goals.FirstOrDefaultAsync(
                item => item.Id == request.GoalId && item.CreatedBy == request.UserId,
                cancellationToken);
            if (goal is null)
            {
                return Result<int>.Failure(Error.Create("Goal was not found."));
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
                    return Result<int>.Failure(Error.Create("Milestone was not found in this goal."));
                }
            }

            var task = new GoalTask
            {
                GoalId = goal.Id,
                MilestoneId = request.MilestoneId,
                Title = request.Title.Trim(),
                Description = GoalTrackingMapper.Normalize(request.Description),
                DueDate = request.DueDate,
                SortOrder = request.SortOrder,
                CreatedBy = request.UserId,
            };
            GoalTrackingMapper.Apply(task, request.Tracking);

            context.GoalTasks.Add(task);
            int rows = await context.SaveChangesAsync(cancellationToken);

            return rows > 0
                ? Result<int>.Success(task.Id)
                : Result<int>.Failure(Error.Create("Failed to create task."));
        }
    }

    [ExcludeFromCodeCoverage]
    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost($"{ApiGroup.V1.Goals}/{{goalId:int}}/tasks", async (
                int goalId,
                [FromBody] Request request,
                ClaimsPrincipal user,
                ISender sender) =>
            {
                Result<int> result = await sender.Send(request with
                {
                    GoalId = goalId,
                    UserId = user.GetCurrentUserId(),
                });
                return result.IsSuccess
                    ? Results.Created($"{ApiGroup.V1.Goals}/{goalId}/tasks/{result.Value}", result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<int>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Add a task directly to a goal or one of its milestones.")
            .WithTags(Tags.Goals)
            .RequireAuthorization();
        }
    }
}
