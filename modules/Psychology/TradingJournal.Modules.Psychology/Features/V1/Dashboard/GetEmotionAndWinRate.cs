using TradingJournal.Modules.Psychology.ViewModel;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Psychology.Features.V1.Dashboard;

public sealed class GetEmotionAndWinRate
{
    internal record Request(int UserId = 0) : IQuery<Result<List<EmotionWinRateViewModel>>>;

    internal sealed class Handler(ITradeProvider tradeProvider, IEmotionTagProvider emotionTagProvider)
        : IQueryHandler<Request, Result<List<EmotionWinRateViewModel>>>
    {
        public async Task<Result<List<EmotionWinRateViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            List<TradeCacheDto> allTrades = await tradeProvider.GetTradesAsync(cancellationToken);
            List<TradeCacheDto> trades = [.. allTrades.Where(t => t.CreatedBy == request.UserId)];
            List<EmotionTagCacheDto> tags = await emotionTagProvider.GetEmotionTagsAsync(cancellationToken);

            var closedTrades = allTrades.Where(t => t.ClosedDate.HasValue && t.Pnl.HasValue).ToList();

            Dictionary<int, (int wins, int total)> tagStats = [];

            foreach (var trade in closedTrades)
            {
                if (trade.EmotionTags != null)
                {
                    foreach (var tagId in trade.EmotionTags)
                    {
                        if (!tagStats.ContainsKey(tagId))
                            tagStats[tagId] = (0, 0);

                        var current = tagStats[tagId];
                        current.total++;
                        if (trade.Pnl > 0)
                            current.wins++;

                        tagStats[tagId] = current;
                    }
                }
            }

            var result = tags
                .Where(t => tagStats.ContainsKey(t.Id))
                .Select(t =>
                {
                    var (wins, total) = tagStats[t.Id];
                    return new EmotionWinRateViewModel
                    {
                        Id = t.Id,
                        Name = t.Name,
                        Label = t.Name,
                        WinRate = (int)Math.Round((double)wins / total * 100),
                        Total = total
                    };
                })
                .Where(x => x.Total >= 1)
                .OrderByDescending(x => x.WinRate)
                .ToList();

            return Result<List<EmotionWinRateViewModel>>.Success(result);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/dashboard"); 

            group.MapGet("emotion-win-rate", async (IMediator mediator) =>
            {
                var result = await mediator.Send(new Request());
                return result;
            })
            .Produces<Result<List<EmotionWinRateViewModel>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Get emotion win rate.")
            .WithDescription("Calculates win rate percentage per emotion tag for closed trades.")
            .WithTags(Tags.Dashboard)
            .RequireAuthorization();
        }
    }
}