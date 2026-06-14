namespace TradingJournal.Modules.Goals.Features.V1;

public sealed class AddMilestone
{
    public sealed record Request(
        int GoalId,
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

            var milestone = new GoalMilestone
            {
                GoalId = goal.Id,
                Title = request.Title.Trim(),
                Description = GoalTrackingMapper.Normalize(request.Description),
                DueDate = request.DueDate,
                SortOrder = request.SortOrder,
                CreatedBy = request.UserId,
            };
            GoalTrackingMapper.Apply(milestone, request.Tracking);

            return await context.ExecuteInTransactionAsync(async ct =>
            {
                context.Milestones.Add(milestone);
                int rows = await context.SaveChangesAsync(ct);

                return rows > 0
                    ? Result<int>.Success(milestone.Id)
                    : Result<int>.Failure(Error.Create("Failed to create milestone."));
            }, cancellationToken);
        }
    }

    [ExcludeFromCodeCoverage]
    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost($"{ApiGroup.V1.Goals}/{{goalId:int}}/milestones", async (
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
                    ? Results.Created($"{ApiGroup.V1.Goals}/{goalId}/milestones/{result.Value}", result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<int>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Add a milestone to a goal.")
            .WithTags(Tags.Goals)
            .RequireAuthorization();
        }
    }
}
