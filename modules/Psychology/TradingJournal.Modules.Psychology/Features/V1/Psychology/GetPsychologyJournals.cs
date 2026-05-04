using TradingJournal.Modules.Psychology.ViewModel;
using TradingJournal.Shared.Common;
using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Psychology.Features.V1.Psychology;

public sealed class GetPsychologyJournals
{
    public record Request(int Page = 1, int PageSize = 10,
        DateTimeOffset? StartDate = null,
        DateTimeOffset? EndDate = null,
        OverallMood? OverallMood = null,
        ConfidentLevel? ConfidentLevel = null,
        List<int>? EmotionTags = null,
        int UserId = 0) : IQuery<Result<PaginationViewModel<PsychologyJournalViewModel>>>;

    public sealed class Handler(IPsychologyDbContext context, ICacheRepository cacheRepository) : IQueryHandler<Request, Result<PaginationViewModel<PsychologyJournalViewModel>>>
    {
        public async Task<Result<PaginationViewModel<PsychologyJournalViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            string cacheKey = $"psychology-journals-{request.ToHashString()}";

            Result<PaginationViewModel<PsychologyJournalViewModel>?> result = await cacheRepository.GetOrCreateAsync(
                cacheKey,
                async (CancellationToken cancellationToken) =>
                {
                    return await GetPsychologyJournalsAsync(request, cancellationToken);
                },
                TimeSpan.FromMinutes(5),
                cancellationToken
            );

            return result.IsSuccess ? Result<PaginationViewModel<PsychologyJournalViewModel>>.Success(result.Value ?? new PaginationViewModel<PsychologyJournalViewModel>()) :
                Result<PaginationViewModel<PsychologyJournalViewModel>>.Failure(result.Errors);
        }

        private async Task<PaginationViewModel<PsychologyJournalViewModel>> GetPsychologyJournalsAsync(Request request, CancellationToken cancellationToken)
        {
            IQueryable<PsychologyJournal> query = context.PsychologyJournals
                .Where(x => x.CreatedBy == request.UserId)
                .Include(x => x.PsychologyJournalEmotions)
                .ThenInclude(x => x.EmotionTag)
                .AsNoTracking();

            if (request.StartDate.HasValue)
            {
                query = query.Where(x => x.Date >= request.StartDate.Value);
            }

            if (request.EndDate.HasValue)
            {
                query = query.Where(x => x.Date <= request.EndDate.Value);
            }

            if (request.OverallMood.HasValue)
            {
                query = query.Where(x => x.OverallMood == request.OverallMood.Value);
            }

            if (request.ConfidentLevel.HasValue)
            {
                query = query.Where(x => x.ConfidentLevel == request.ConfidentLevel.Value);
            }

            if (request.EmotionTags != null && request.EmotionTags.Count != 0)
            {
                query = query.Where(x => x.PsychologyJournalEmotions.Any(e => request.EmotionTags.Contains(e.EmotionTagId)));
            }

            int totalCount = await query.CountAsync(cancellationToken);
            List<PsychologyJournal> psychologyJournals = await query
                .OrderByDescending(x => x.Date)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            PaginationViewModel<PsychologyJournalViewModel> paginationViewModel = new()
            {
                Values = [.. psychologyJournals.Select(x => new PsychologyJournalViewModel
                {
                    Id = x.Id,
                    Date = x.Date,
                    TodayTradingReview = x.TodayTradingReview,
                    OverallMood = x.OverallMood,
                    ConfidentLevel = x.ConfidentLevel,
                    EmotionTags = [.. x.PsychologyJournalEmotions.Select(e => new PsychologyJournalEmotionViewModel
                    {
                        Id = e.Id,
                        Name = e.EmotionTag.Name
                    })]
                })],
                HasMore = totalCount > request.Page * request.PageSize,
                TotalItems = totalCount
            };

            return paginationViewModel;
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/psychology-journals");

            group.MapPost("/search", async ([FromBody] Request request, ClaimsPrincipal user, ISender sender) =>
            {
                Result<PaginationViewModel<PsychologyJournalViewModel>> result = await sender.Send(request with { UserId = user.GetCurrentUserId() });
                return result;
            })
            .Produces<Result<PaginationViewModel<PsychologyJournalViewModel>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Search psychology journals.")
            .WithDescription("Searches psychology journals.")
            .WithTags(Tags.PsychologyJournal)
            .RequireAuthorization();
        }
    }
}
