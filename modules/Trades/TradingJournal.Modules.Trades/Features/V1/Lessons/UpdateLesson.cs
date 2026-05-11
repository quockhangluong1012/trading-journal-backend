namespace TradingJournal.Modules.Trades.Features.V1.Lessons;

public sealed class UpdateLesson
{
    public sealed record Request(
        int Id,
        string Title,
        string Content,
        LessonCategory Category,
        LessonSeverity Severity,
        LessonStatus Status,
        string? KeyTakeaway,
        string? ActionItems,
        int ImpactScore,
        List<string>? Tags) : ICommand<Result>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Lesson ID must be greater than 0.");

            RuleFor(x => x.Title)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Title is required.")
                .MaximumLength(200).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Title must not exceed 200 characters.");

            RuleFor(x => x.Content)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Content is required.");

            RuleFor(x => x.Category)
                .Must(Enum.IsDefined).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Category must be a valid LessonCategory value.");

            RuleFor(x => x.Severity)
                .Must(Enum.IsDefined).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Severity must be a valid LessonSeverity value.");

            RuleFor(x => x.Status)
                .Must(Enum.IsDefined).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Status must be a valid LessonStatus value.");

            RuleFor(x => x.ImpactScore)
                .InclusiveBetween(1, 10).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Impact score must be between 1 and 10.");

            RuleFor(x => x.Tags)
                .Must(tags => tags is null || LessonTagSerializer.NormalizeTags(tags).Count <= 8)
                .WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("A lesson can have at most 8 tags.");

            RuleForEach(x => x.Tags)
                .MaximumLength(32).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Each tag must not exceed 32 characters.");
        }
    }

    public sealed class Handler(ITradeDbContext context, IHttpContextAccessor httpContextAccessor)
        : ICommandHandler<Request, Result>
    {
        public async Task<Result> Handle(Request request, CancellationToken cancellationToken)
        {
            int userId = httpContextAccessor.HttpContext?.User.GetCurrentUserId() ?? 0;

            LessonLearned? lesson = await context.LessonsLearned
                .FirstOrDefaultAsync(l => l.Id == request.Id && l.CreatedBy == userId, cancellationToken);

            if (lesson is null)
            {
                return Result.Failure(Error.Create("Lesson not found."));
            }

            lesson.Title = request.Title;
            lesson.Content = request.Content;
            lesson.Category = request.Category;
            lesson.Severity = request.Severity;
            lesson.Status = request.Status;
            lesson.KeyTakeaway = request.KeyTakeaway;
            lesson.ActionItems = request.ActionItems;
            lesson.ImpactScore = request.ImpactScore;
            lesson.Tags = request.Tags ?? [];

            await context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Lessons);

            group.MapPut("/{id:int}", async (int id, [FromBody] Request request, ISender sender) =>
            {
                if (id != request.Id)
                {
                    return Results.BadRequest("Route ID does not match request body ID.");
                }

                Result result = await sender.Send(request);

                return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result);
            })
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Update an existing lesson.")
            .WithDescription("Updates the lesson details including status, severity, and content.")
            .WithTags(Tags.Lessons)
            .RequireAuthorization();
        }
    }
}
