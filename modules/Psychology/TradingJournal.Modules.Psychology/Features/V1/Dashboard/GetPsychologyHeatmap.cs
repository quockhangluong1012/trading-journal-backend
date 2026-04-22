using TradingJournal.Modules.Psychology.ViewModel;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Psychology.Features.V1.Dashboard;

public sealed class GetPsychologyHeatmap
{
    internal record Request(int UserId = 0) : IQuery<Result<List<PsychologyHeatmapViewModel>>>;

    internal sealed class Handler(ITradeProvider tradeProvider, IEmotionTagProvider emotionTagProvider)
        : IQueryHandler<Request, Result<List<PsychologyHeatmapViewModel>>>
    {
        public async Task<Result<List<PsychologyHeatmapViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            List<TradeCacheDto> trades = await tradeProvider.GetTradesAsync(request.UserId, cancellationToken);
            List<EmotionTagCacheDto> tags = await emotionTagProvider.GetEmotionTagsAsync(cancellationToken);

            // Only consider closed trades that have a Pnl
            var closedTrades = trades.Where(t => t.ClosedDate.HasValue && t.Pnl.HasValue && t.EmotionTags != null && t.EmotionTags.Count > 0).ToList();

            Dictionary<int, EmotionStats> tagStats = new();

            foreach (var trade in closedTrades)
            {
                foreach (var tagId in trade.EmotionTags!)
                {
                    if (!tagStats.ContainsKey(tagId))
                        tagStats[tagId] = new EmotionStats();

                    var current = tagStats[tagId];
                    current.TotalPnl += trade.Pnl!.Value;
                    current.Count++;

                    if (trade.Pnl!.Value > 0)
                    {
                        current.Wins++;
                        current.WinPnl += trade.Pnl.Value;
                    }
                    else
                    {
                        current.Losses++;
                        current.LossPnl += trade.Pnl.Value;
                    }
                }
            }

            var result = tags
                .Where(t => tagStats.ContainsKey(t.Id))
                .Select(t =>
                {
                    var stat = tagStats[t.Id];
                    return new PsychologyHeatmapViewModel
                    {
                        Id = t.Id,
                        Name = t.Name,
                        Label = t.Name,
                        AvgPnl = stat.Count > 0 ? stat.TotalPnl / stat.Count : 0,
                        TotalPnl = stat.TotalPnl,
                        Count = stat.Count,
                        Wins = stat.Wins,
                        Losses = stat.Losses,
                        WinRate = stat.Count > 0 ? (int)Math.Round((double)stat.Wins / stat.Count * 100) : 0,
                        AvgWinPnl = stat.Wins > 0 ? stat.WinPnl / stat.Wins : 0,
                        AvgLossPnl = stat.Losses > 0 ? stat.LossPnl / stat.Losses : 0
                    };
                })
                .OrderByDescending(x => x.AvgPnl)
                .ToList();

            return Result<List<PsychologyHeatmapViewModel>>.Success(result);
        }

        private class EmotionStats
        {
            public decimal TotalPnl { get; set; }
            public int Count { get; set; }
            public int Wins { get; set; }
            public int Losses { get; set; }
            public decimal WinPnl { get; set; }
            public decimal LossPnl { get; set; }
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/dashboard");
            
            group.MapGet("psychology-heatmap", async (ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request(user.GetCurrentUserId()));
                return result;
            })
            .Produces<Result<List<PsychologyHeatmapViewModel>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Get psychology heatmap.")
            .WithDescription("Calculates PnL and win statistics per emotion tag.")
            .WithTags(Tags.Dashboard)
            .RequireAuthorization();
        }
    }
}