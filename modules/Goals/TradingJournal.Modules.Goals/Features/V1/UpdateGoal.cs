namespace TradingJournal.Modules.Goals.Features.V1;

public sealed class UpdateGoal
{
    public sealed record Request(
        string Title,
        string? Description,
        DateTime? StartDate,
        DateTime? DueDate,
        TrackingInput? Tracking,
        int GoalId = 0,
        int UserId = 0) : ICommand<Result>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).MaximumLength(2000);
            RuleFor(x => x.DueDate)
                .GreaterThanOrEqualTo(x => x.StartDate)
                .When(x => x.StartDate.HasValue && x.DueDate.HasValue);
            RuleFor(x => x.Tracking!).SetValidator(new TrackingInputValidator()).When(x => x.Tracking is not null);
        }
    }

    public sealed class Handler(IGoalDbContext context) : ICommandHandler<Request, Result>
    {
        public async Task<Result> Handle(Request request, CancellationToken cancellationToken)
        {
            Goal? goal = await context.Goals.FirstOrDefaultAsync(
                item => item.Id == request.GoalId && item.CreatedBy == request.UserId,
                cancellationToken);
            if (goal is null)
            {
                return Result.Failure(Error.Create("Goal was not found."));
            }

            goal.Title = request.Title.Trim();
            goal.Description = GoalTrackingMapper.Normalize(request.Description);
            goal.StartDate = request.StartDate;
            goal.DueDate = request.DueDate;

            if (request.Tracking is not null)
            {
                GoalTrackingMapper.Reconfigure(goal, request.Tracking);
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
            app.MapPatch($"{ApiGroup.V1.Goals}/{{goalId:int}}", async (
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
            .WithSummary("Update a goal's metadata and (optionally) its tracking config.")
            .WithTags(Tags.Goals)
            .RequireAuthorization();
        }
    }
}
