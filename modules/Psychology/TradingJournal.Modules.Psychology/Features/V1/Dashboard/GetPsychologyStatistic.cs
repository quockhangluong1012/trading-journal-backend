using TradingJournal.Modules.Psychology.ViewModel;
using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Psychology.Features.V1.Dashboard;

public sealed class GetPsychologyStatistic
{
    internal record Request(int UserId = 0) : IQuery<Result<PsychologyStatisticViewModel>>;

    internal sealed class Handler(IPsychologyDbContext context, ICacheRepository cacheRepository) : IQueryHandler<Request, Result<PsychologyStatisticViewModel>>
    {
        public async Task<Result<PsychologyStatisticViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            string cacheKey = $"psychology-statistic-{request.ToHashString()}";
            Result<PsychologyStatisticViewModel?> result = await cacheRepository.GetOrCreateAsync(
                cacheKey,
                async cancellationToken =>
                {
                    return await GetPsychologyStatisticAsync(request, cancellationToken);
                },
                TimeSpan.FromMinutes(5),
                cancellationToken
            );
            return result.IsSuccess ? Result<PsychologyStatisticViewModel>.Success(result.Value ?? new PsychologyStatisticViewModel()) :
                Result<PsychologyStatisticViewModel>.Failure(result.Errors);
        }

        private async Task<PsychologyStatisticViewModel> GetPsychologyStatisticAsync(Request request, CancellationToken cancellationToken)
        {
            IQueryable<PsychologyJournal> query = context.PsychologyJournals
                .Include(x => x.PsychologyJournalEmotions)
                .ThenInclude(x => x.EmotionTag)
                .AsNoTracking()
                .Where(x => x.CreatedBy == request.UserId);

            List<PsychologyJournal> psychologyJournals = await query.ToListAsync(cancellationToken);

            string topEmotion = psychologyJournals
                .SelectMany(x => x.PsychologyJournalEmotions)
                .GroupBy(x => x.EmotionTag.Name)
                .Select(g => new { EmotionTag = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .FirstOrDefault()?.EmotionTag ?? string.Empty;

            int totalEmotions = psychologyJournals
                .SelectMany(x => x.PsychologyJournalEmotions).Count();

            int positiveCount = psychologyJournals
                .SelectMany(x => x.PsychologyJournalEmotions)
                .Count(x => x.EmotionTag.EmotionType == EmotionType.Positive);

            PsychologyStatisticViewModel statistic = new()
            {
                JournalEntries = psychologyJournals.Count,
                AvgConfidence = psychologyJournals.Count != 0 ? psychologyJournals.Average(x => (int)x.ConfidentLevel) : 0,
                TopEmotion = topEmotion,
                PsychologyScore = totalEmotions > 0 ? (double)positiveCount / totalEmotions : 0
            };
            return statistic;
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/dashboard");

            group.MapGet("/statistic", async (ClaimsPrincipal user, ISender sender) =>
            {
                Result<PsychologyStatisticViewModel> result = await sender.Send(new Request(user.GetCurrentUserId()));
                return result;
            })
            .Produces<Result<PsychologyStatisticViewModel>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Get psychology statistic.")
            .WithDescription("Gets psychology statistic based on the psychology journals in a specific period.")
            .WithTags("Dashboard")
            .RequireAuthorization();
        }
    }
}
