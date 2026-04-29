using TradingJournal.Shared.Common;

namespace TradingJournal.Modules.Trades.Features.V1.Lessons;

public sealed class GetLessons
{
    public class Request : IQuery<Result<PaginationViewModel<LessonLearnedViewModel>>>
    {
        public LessonCategory? Category { get; set; }

        public LessonSeverity? Severity { get; set; }

        public LessonStatus? Status { get; set; }

        public string? SearchTerm { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 10;

        public int UserId { get; set; }
    }

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Page)
                .GreaterThan(0).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Page must be greater than 0.");

            RuleFor(x => x.PageSize)
                .GreaterThan(0).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Page size must be greater than 0.");
        }
    }

    public sealed class Handler(ITradeDbContext context)
        : IQueryHandler<Request, Result<PaginationViewModel<LessonLearnedViewModel>>>
    {
        public async Task<Result<PaginationViewModel<LessonLearnedViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            IQueryable<LessonLearned> query = context.LessonsLearned
                .Where(l => l.CreatedBy == request.UserId)
                .AsNoTracking();

            if (request.Category.HasValue)
            {
                query = query.Where(l => l.Category == request.Category.Value);
            }

            if (request.Severity.HasValue)
            {
                query = query.Where(l => l.Severity == request.Severity.Value);
            }

            if (request.Status.HasValue)
            {
                query = query.Where(l => l.Status == request.Status.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                string term = request.SearchTerm.Trim();
                query = query.Where(l => l.Title.Contains(term) || l.KeyTakeaway!.Contains(term));
            }

            int totalItems = await query.CountAsync(cancellationToken);

            List<LessonLearned> lessons = await query
                .OrderByDescending(l => l.CreatedDate)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            List<int> lessonIds = [.. lessons.Select(l => l.Id)];

            // Batch-fetch linked trade counts (single query, no N+1)
            Dictionary<int, int> linkCounts = await context.LessonTradeLinks
                .AsNoTracking()
                .Where(ltl => lessonIds.Contains(ltl.LessonLearnedId))
                .GroupBy(ltl => ltl.LessonLearnedId)
                .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);

            List<LessonLearnedViewModel> viewModels = [.. lessons.Select(l => new LessonLearnedViewModel
            {
                Id = l.Id,
                Title = l.Title,
                Category = l.Category,
                Severity = l.Severity,
                Status = l.Status,
                KeyTakeaway = l.KeyTakeaway,
                ImpactScore = l.ImpactScore,
                LinkedTradesCount = linkCounts.GetValueOrDefault(l.Id, 0),
                CreatedDate = l.CreatedDate
            })];

            return Result<PaginationViewModel<LessonLearnedViewModel>>.Success(new PaginationViewModel<LessonLearnedViewModel>
            {
                TotalItems = totalItems,
                HasMore = (request.Page * request.PageSize) < totalItems,
                Values = viewModels
            });
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Lessons);

            group.MapPost("/search", async ([FromBody] Request request, ClaimsPrincipal user, ISender sender) =>
            {
                request.UserId = user.GetCurrentUserId();
                Result<PaginationViewModel<LessonLearnedViewModel>> result = await sender.Send(request);

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<PaginationViewModel<LessonLearnedViewModel>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Search lessons learned.")
            .WithDescription("Paginated search with optional category, severity, status, and text filters.")
            .WithTags(Tags.Lessons)
            .RequireAuthorization();
        }
    }
}
