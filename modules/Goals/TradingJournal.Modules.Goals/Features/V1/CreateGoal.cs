namespace TradingJournal.Modules.Goals.Features.V1;

public sealed class CreateGoal
{
    public sealed record Request(
        string Title,
        string? Description,
        DateTime? StartDate,
        DateTime? DueDate,
        TrackingInput Tracking,
        int UserId = 0) : ICommand<Result<int>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).MaximumLength(2000);
            RuleFor(x => x.Tracking).NotNull().SetValidator(new TrackingInputValidator());
            RuleFor(x => x.DueDate)
                .GreaterThanOrEqualTo(x => x.StartDate)
                .When(x => x.StartDate.HasValue && x.DueDate.HasValue);
        }
    }

    public sealed class Handler(IGoalDbContext context) : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            if (request.UserId <= 0)
            {
                return Result<int>.Failure(Error.Create("Current user is required."));
            }

            var goal = new Goal
            {
                Title = request.Title.Trim(),
                Description = GoalTrackingMapper.Normalize(request.Description),
                StartDate = request.StartDate,
                DueDate = request.DueDate,
                CreatedBy = request.UserId,
            };
            GoalTrackingMapper.Apply(goal, request.Tracking);

            context.Goals.Add(goal);
            int rows = await context.SaveChangesAsync(cancellationToken);

            return rows > 0
                ? Result<int>.Success(goal.Id)
                : Result<int>.Failure(Error.Create("Failed to create goal."));
        }
    }

    [ExcludeFromCodeCoverage]
    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost(ApiGroup.V1.Goals, async (
                [FromBody] Request request,
                ClaimsPrincipal user,
                ISender sender) =>
            {
                Result<int> result = await sender.Send(request with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess
                    ? Results.Created($"{ApiGroup.V1.Goals}/{result.Value}", result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<int>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Create a manually or metrically tracked goal.")
            .WithTags(Tags.Goals)
            .RequireAuthorization();
        }
    }
}
