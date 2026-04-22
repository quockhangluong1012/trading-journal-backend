using TradingJournal.Modules.Psychology.ViewModel;
using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Psychology.Features.V1.Dashboard;

public sealed class GetEmotionDistribution
{
    internal record Request(int UserId = 0) : IQuery<Result<List<EmotionDistributionViewModel>>>;

    internal sealed class Handler(ITradeProvider tradeProvider, IEmotionTagProvider emotionTagProvider)
        : IQueryHandler<Request, Result<List<EmotionDistributionViewModel>>>
    {
        public async Task<Result<List<EmotionDistributionViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            List<TradeCacheDto> trades = await tradeProvider.GetTradesAsync(request.UserId, cancellationToken);
            List<EmotionTagCacheDto> tags = await emotionTagProvider.GetEmotionTagsAsync(cancellationToken);

            var tagDict = tags.ToDictionary(t => t.Id, t => t);

            int posCount = 0;
            int negCount = 0;
            int neuCount = 0;

            foreach (var trade in trades)
            {
                if (trade.EmotionTags != null)
                {
                    foreach (var tagId in trade.EmotionTags)
                    {
                        if (tagDict.TryGetValue(tagId, out var tag))
                        {
                            if (tag.EmotionType == EmotionType.Positive) posCount++;
                            else if (tag.EmotionType == EmotionType.Negative) negCount++;
                            else neuCount++;
                        }
                    }
                }
            }

            var result = new List<EmotionDistributionViewModel>();

            if (posCount > 0)
                result.Add(new EmotionDistributionViewModel { Name = "Positive", Value = posCount, Fill = "#22c55e" });
            if (negCount > 0)
                result.Add(new EmotionDistributionViewModel { Name = "Negative", Value = negCount, Fill = "#ef4444" });
            if (neuCount > 0)
                result.Add(new EmotionDistributionViewModel { Name = "Neutral", Value = neuCount, Fill = "#3b82f6" });

            return Result<List<EmotionDistributionViewModel>>.Success(result);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/dashboard");
            
            group.MapGet("emotion-distribution", async (ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request(user.GetCurrentUserId()));
                return result;
            })
            .Produces<Result<List<EmotionDistributionViewModel>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Get emotion distribution.")
            .WithDescription("Groups emotion tags into positive, negative, and neutral categories.")
            .WithTags(Tags.Dashboard)
            .RequireAuthorization();
        }
    }
}