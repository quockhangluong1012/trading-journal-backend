using TradingJournal.Shared.Common;
using TradingJournal.Modules.Trades.Common;

namespace TradingJournal.Modules.Trades.Features.V1.Lessons;

public sealed class GetLessons
{
    public class Request : IQuery<Result<PaginationViewModel<LessonLearnedViewModel>>>
    {
        public LessonCategory? Category { get; set; }

        public LessonSeverity? Severity { get; set; }

        public LessonStatus? Status { get; set; }

        public int? MinimumImpactScore { get; set; }

        public bool LinkedTradesOnly { get; set; }

        public LessonSortOption SortBy { get; set; } = LessonSortOption.Newest;

        public List<string>? Tags { get; set; }

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

            RuleFor(x => x.SearchTerm)
                .MaximumLength(200).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Search term must not exceed 200 characters.")
                .When(x => !string.IsNullOrWhiteSpace(x.SearchTerm));

            RuleFor(x => x.MinimumImpactScore)
                .InclusiveBetween(1, 10).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Minimum impact score must be between 1 and 10.")
                .When(x => x.MinimumImpactScore.HasValue);

            RuleFor(x => x.SortBy)
                .Must(Enum.IsDefined).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Sort option must be a valid LessonSortOption value.");
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

            if (request.MinimumImpactScore.HasValue)
            {
                query = query.Where(l => l.ImpactScore >= request.MinimumImpactScore.Value);
            }

            if (request.LinkedTradesOnly)
            {
                query = query.Where(l => l.LessonTradeLinks.Any(link => !link.IsDisabled));
            }

            List<string> normalizedTags = LessonTagSerializer.NormalizeTags(request.Tags);

            foreach (string tag in normalizedTags)
            {
                string tagToken = LessonTagSerializer.BuildContainsToken(tag);

                if (!string.IsNullOrWhiteSpace(tagToken))
                {
                    query = query.Where(l => l.TagsText.Contains(tagToken));
                }
            }

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                string term = request.SearchTerm.Trim();
                query = query.Where(l =>
                    l.Title.Contains(term) ||
                    l.Content.Contains(term) ||
                    (l.KeyTakeaway != null && l.KeyTakeaway.Contains(term)) ||
                    (l.ActionItems != null && l.ActionItems.Contains(term)) ||
                    l.TagsText.Contains(term));
            }

            int totalItems = await query.CountAsync(cancellationToken);

            List<LessonLearned> lessons = await ApplySorting(query, request.SortBy)
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
                Tags = l.Tags,
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

        private static IOrderedQueryable<LessonLearned> ApplySorting(IQueryable<LessonLearned> query, LessonSortOption sortBy)
        {
            return sortBy switch
            {
                LessonSortOption.Oldest => query
                    .OrderBy(l => l.CreatedDate)
                    .ThenBy(l => l.Id),
                LessonSortOption.HighestImpact => query
                    .OrderByDescending(l => l.ImpactScore)
                    .ThenByDescending(l => l.CreatedDate)
                    .ThenByDescending(l => l.Id),
                LessonSortOption.LowestImpact => query
                    .OrderBy(l => l.ImpactScore)
                    .ThenByDescending(l => l.CreatedDate)
                    .ThenByDescending(l => l.Id),
                LessonSortOption.MostLinkedTrades => query
                    .OrderByDescending(l => l.LessonTradeLinks.Count(link => !link.IsDisabled))
                    .ThenByDescending(l => l.ImpactScore)
                    .ThenByDescending(l => l.CreatedDate)
                    .ThenByDescending(l => l.Id),
                LessonSortOption.TitleAsc => query
                    .OrderBy(l => l.Title)
                    .ThenByDescending(l => l.CreatedDate)
                    .ThenByDescending(l => l.Id),
                _ => query
                    .OrderByDescending(l => l.CreatedDate)
                    .ThenByDescending(l => l.Id),
            };
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
            .WithDescription("Paginated search with optional category, severity, study status, impact, linked-trade, tag, text, and sort filters.")
            .WithTags(Tags.Lessons)
            .RequireAuthorization();
        }
    }
}
