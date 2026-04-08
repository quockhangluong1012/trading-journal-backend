using TradingJournal.Modules.Psychology.ViewModel;
using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Psychology.Features.V1.Dashboard;

public sealed class GetMoodAndConfidenceTrend
{
    internal record Request(int UserId = 0) : IQuery<Result<List<MoodAndConfidenceTrendViewModel>>>;

    internal sealed class Handler(IPsychologyDbContext context, ICacheRepository cacheRepository) 
        : IQueryHandler<Request, Result<List<MoodAndConfidenceTrendViewModel>>>
    {
        public async Task<Result<List<MoodAndConfidenceTrendViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            string cacheKey = $"psychology-mood-trend-{request.ToHashString()}";
            
            Result<List<MoodAndConfidenceTrendViewModel>?> result = await cacheRepository.GetOrCreateAsync(
                cacheKey,
                async ct =>
                {
                    var journals = await context.PsychologyJournals
                        .AsNoTracking()
                        .Where(x => x.CreatedBy == request.UserId)
                        .OrderBy(x => x.Date)
                        .ToListAsync(ct);

                    return journals.Select(j => new MoodAndConfidenceTrendViewModel
                    {
                        Date = j.Date,
                        Mood = (int)j.OverallMood,
                        Confidence = (int)j.ConfidentLevel
                    }).ToList();
                },
                TimeSpan.FromMinutes(5),
                cancellationToken
            );

            return result.IsSuccess ? Result<List<MoodAndConfidenceTrendViewModel>>.Success(result.Value ?? []) :
                Result<List<MoodAndConfidenceTrendViewModel>>.Failure(result.Errors);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/dashboard");
            
            group.MapGet("mood-confidence-trend", async (IMediator mediator) =>
            {
                var result = await mediator.Send(new Request());
                return result;
            })
            .Produces<Result<List<MoodAndConfidenceTrendViewModel>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Get mood and confidence trend.")
            .WithDescription("Gets the trend of overall mood and confidence level over time.")
            .WithTags(Tags.Dashboard)
            .RequireAuthorization();
        }
    }
}