namespace TradingJournal.Modules.Trades.Features.V1.Lessons;

public sealed class CreateLesson
{
    public sealed record Request(
        string Title,
        string Content,
        LessonCategory Category,
        LessonSeverity Severity,
        string? KeyTakeaway,
        string? ActionItems,
        int ImpactScore,
        List<int>? LinkedTradeIds) : ICommand<Result<int>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
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
                .Cascade(CascadeMode.Stop)
                .Must(Enum.IsDefined).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Category must be a valid LessonCategory value.");

            RuleFor(x => x.Severity)
                .Cascade(CascadeMode.Stop)
                .Must(Enum.IsDefined).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Severity must be a valid LessonSeverity value.");

            RuleFor(x => x.ImpactScore)
                .Cascade(CascadeMode.Stop)
                .InclusiveBetween(1, 10).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Impact score must be between 1 and 10.");

            RuleFor(x => x.KeyTakeaway)
                .MaximumLength(500).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Key takeaway must not exceed 500 characters.");

            RuleFor(x => x.ActionItems)
                .MaximumLength(2000).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Action items must not exceed 2000 characters.");
        }
    }

    public sealed class Handler(ITradeDbContext context, IHttpContextAccessor httpContextAccessor)
        : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            int userId = httpContextAccessor.HttpContext?.User.GetCurrentUserId() ?? 0;
            if (userId <= 0)
            {
                return Result<int>.Failure(Error.Create("Unauthorized."));
            }

            List<int> distinctTradeIds = request.LinkedTradeIds is { Count: > 0 }
                ? [.. request.LinkedTradeIds.Distinct()]
                : [];

            if (distinctTradeIds.Count > 0)
            {
                int accessibleCount = await context.TradeHistories
                    .AsNoTracking()
                    .CountAsync(t => distinctTradeIds.Contains(t.Id) && t.CreatedBy == userId, cancellationToken);

                if (accessibleCount != distinctTradeIds.Count)
                {
                    return Result<int>.Failure(Error.Create("One or more trade IDs are invalid or do not belong to the current user."));
                }
            }

            try
            {
                return await context.ExecuteInTransactionAsync(async ct =>
                {
                    LessonLearned lesson = new()
                    {
                        Id = 0,
                        Title = request.Title,
                        Content = request.Content,
                        Category = request.Category,
                        Severity = request.Severity,
                        KeyTakeaway = request.KeyTakeaway,
                        ActionItems = request.ActionItems,
                        ImpactScore = request.ImpactScore,
                        Status = LessonStatus.New
                    };

                    await context.LessonsLearned.AddAsync(lesson, ct);

                    if (distinctTradeIds.Count > 0)
                    {
                        await context.LessonTradeLinks.AddRangeAsync(distinctTradeIds.Select(tradeId => new LessonTradeLink
                        {
                            Id = 0,
                            TradeHistoryId = tradeId,
                            LessonLearned = lesson
                        }), ct);
                    }

                    int insertedRows = await context.SaveChangesAsync(ct);

                    return insertedRows > 0
                        ? Result<int>.Success(lesson.Id)
                        : Result<int>.Failure(Error.Create("Failed to create lesson."));
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                return Result<int>.Failure(Error.Create(ex.Message));
            }
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Lessons);

            group.MapPost("/", async ([FromBody] Request request, ClaimsPrincipal user, ISender sender) =>
            {
                Result<int> result = await sender.Send(request);

                return result.IsSuccess
                    ? Results.Created($"{ApiGroup.V1.Lessons}/{result.Value}", result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<int>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Create a new lesson learned.")
            .WithDescription("Creates a new lesson learned entry with optional trade links.")
            .WithTags(Tags.Lessons)
            .RequireAuthorization();
        }
    }
}
