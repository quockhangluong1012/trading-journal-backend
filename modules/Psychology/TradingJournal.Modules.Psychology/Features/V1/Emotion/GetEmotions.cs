using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Psychology.Features.V1.Emotion;

public sealed class GetEmotions
{
    public sealed class Request : IQuery<Result<List<EmotionTagCacheDto>>>
    {
    }

    public sealed class Handler(IEmotionTagProvider emotionTagProvider) : IQueryHandler<Request, Result<List<EmotionTagCacheDto>>>
    {
        public async Task<Result<List<EmotionTagCacheDto>>> Handle(Request request, CancellationToken cancellationToken)
        {
            List<EmotionTagCacheDto> emotions = await emotionTagProvider.GetEmotionTagsAsync(cancellationToken);

            return emotions.Count > 0
                ? Result<List<EmotionTagCacheDto>>.Success(emotions)
                : Result<List<EmotionTagCacheDto>>.Failure(Error.NotFound);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/emotions");

            group.MapGet("/", async (ISender sender) =>
            {
                Result<List<EmotionTagCacheDto>> result = await sender.Send(new Request());
                return result;
            })
            .Produces<Result<List<EmotionTagCacheDto>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Get all emotion tags.")
            .WithDescription("Gets all emotion tags")
            .WithTags(Tags.Emotions);
        }
    }
}
