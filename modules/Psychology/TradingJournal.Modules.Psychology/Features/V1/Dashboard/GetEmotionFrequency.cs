using TradingJournal.Modules.Psychology.ViewModel;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Psychology.Features.V1.Dashboard;

public sealed class GetEmotionFrequency
{
    public record Request(int UserId = 0) : IQuery<Result<List<EmotionFrequencyViewModel>>>;

    public sealed class Handler(ITradeProvider tradeProvider, IEmotionTagProvider emotionTagProvider)
        : IQueryHandler<Request, Result<List<EmotionFrequencyViewModel>>>
    {
        public async Task<Result<List<EmotionFrequencyViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            List<TradeCacheDto> trades = await tradeProvider.GetTradesAsync(request.UserId, cancellationToken);
            List<EmotionTagCacheDto> tags = await emotionTagProvider.GetEmotionTagsAsync(cancellationToken);

            Dictionary<int, int> freq = [];

            foreach (var trade in trades)
            {
                if (trade.EmotionTags != null)
                {
                    foreach (var tagId in trade.EmotionTags)
                    {
                        if (!freq.ContainsKey(tagId))
                            freq[tagId] = 0;
                        freq[tagId]++;
                    }
                }
            }

            var result = tags
                .Where(t => freq.ContainsKey(t.Id))
                .Select(t => new EmotionFrequencyViewModel
                {
                    Id = t.Id,
                    Name = t.Name,
                    Label = t.Name,
                    Count = freq[t.Id]
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            return Result<List<EmotionFrequencyViewModel>>.Success(result);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/dashboard");
            
            group.MapGet("emotion-frequency", async (ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request(user.GetCurrentUserId()));
                return result;
            })
            .Produces<Result<List<EmotionFrequencyViewModel>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Get emotion frequency.")
            .WithDescription("Calculates how often each emotion appears across all trades.")
            .WithTags(Tags.Dashboard)
            .RequireAuthorization();
        }
    }
}