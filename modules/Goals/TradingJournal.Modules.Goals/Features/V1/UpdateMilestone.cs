namespace TradingJournal.Modules.Goals.Features.V1;

public sealed class UpdateMilestone
{
    public sealed record Request(
        string Title,
        string? Description,
        DateTime? DueDate,
        TrackingInput? Tracking,
        int GoalId = 0,
        int MilestoneId = 0,
        int UserId = 0) : ICommand<Result>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).MaximumLength(2000);
            RuleFor(x => x.Tracking!).SetValidator(new TrackingInputValidator()).When(x => x.Tracking is not null);
        }
    }

    public sealed class Handler(IGoalDbContext context) : ICommandHandler<Request, Result>
    {
        public async Task<Result> Handle(Request request, CancellationToken cancellationToken)
        {
            GoalMilestone? milestone = await context.Milestones.FirstOrDefaultAsync(
                item => item.Id == request.MilestoneId
                    && item.GoalId == request.GoalId
                    && item.CreatedBy == request.UserId,
                cancellationToken);
            if (milestone is null)
            {
                return Result.Failure(Error.Create("Milestone was not found."));
            }

            milestone.Title = request.Title.Trim();
            milestone.Description = GoalTrackingMapper.Normalize(request.Description);
            milestone.DueDate = request.DueDate;

            if (request.Tracking is not null)
            {
                GoalTrackingMapper.Reconfigure(milestone, request.Tracking);
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
            app.MapPatch($"{ApiGroup.V1.Goals}/{{goalId:int}}/milestones/{{milestoneId:int}}", async (
                int goalId,
                int milestoneId,
                [FromBody] Request request,
                ClaimsPrincipal user,
                ISender sender) =>
            {
                Result result = await sender.Send(request with
                {
                    GoalId = goalId,
                    MilestoneId = milestoneId,
                    UserId = user.GetCurrentUserId(),
                });
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Update a milestone's metadata and (optionally) its tracking config.")
            .WithTags(Tags.Goals)
            .RequireAuthorization();
        }
    }
}
