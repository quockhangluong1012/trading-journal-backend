using TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Modules.RiskManagement.Features.V1;

public sealed class GetRiskHeatmap
{
    internal sealed record Request(int UserId = 0) : IQuery<Result<RiskHeatmapViewModel>>;

    internal sealed record RiskHeatmapViewModel(
        List<AssetExposure> ByAsset,
        DirectionExposure ByDirection,
        decimal TotalExposure);

    internal sealed record AssetExposure(string Asset, int Count, string Direction, decimal Pnl);
    internal sealed record DirectionExposure(int LongCount, int ShortCount, decimal LongPnl, decimal ShortPnl);

    internal sealed class Handler(ITradeProvider tradeProvider) : IQueryHandler<Request, Result<RiskHeatmapViewModel>>
    {
        public async Task<Result<RiskHeatmapViewModel>> Handle(Request request, CancellationToken ct)
        {
            var trades = await tradeProvider.GetTradesAsync(request.UserId, ct);
            var openTrades = trades.Where(t => t.Status == TradeStatus.Open).ToList();

            var byAsset = openTrades
                .GroupBy(t => t.Asset)
                .Select(g => new AssetExposure(
                    g.Key, g.Count(),
                    g.All(t => t.Position == PositionType.Long) ? "Long" :
                    g.All(t => t.Position == PositionType.Short) ? "Short" : "Mixed",
                    g.Sum(t => t.Pnl ?? 0)))
                .OrderByDescending(a => a.Count)
                .ToList();

            int longCount = openTrades.Count(t => t.Position == PositionType.Long);
            int shortCount = openTrades.Count(t => t.Position == PositionType.Short);
            decimal longPnl = openTrades.Where(t => t.Position == PositionType.Long).Sum(t => t.Pnl ?? 0);
            decimal shortPnl = openTrades.Where(t => t.Position == PositionType.Short).Sum(t => t.Pnl ?? 0);

            return Result<RiskHeatmapViewModel>.Success(new(
                byAsset,
                new DirectionExposure(longCount, shortCount, Math.Round(longPnl, 2), Math.Round(shortPnl, 2)),
                Math.Round(openTrades.Sum(t => t.Pnl ?? 0), 2)));
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGroup("api/v1/risk").MapGet("/heatmap", async (ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request() with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result<RiskHeatmapViewModel>>(StatusCodes.Status200OK)
            .WithSummary("Get risk heatmap showing current exposure.")
            .WithTags(Tags.RiskManagement).RequireAuthorization();
        }
    }
}
