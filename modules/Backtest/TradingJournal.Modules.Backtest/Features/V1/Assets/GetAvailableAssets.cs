namespace TradingJournal.Modules.Backtest.Features.V1.Assets;

/// <summary>
/// Public endpoint to list all available assets for backtesting that are ready (synced).
/// </summary>
public static class GetAvailableAssets
{
    public sealed record Request() : IQuery<Result<List<AssetDto>>>;

    public sealed record AssetDto(
        int Id,
        string DisplayName,
        string Symbol,
        string Category,
        DateTime DataStartDate,
        DateTime? DataEndDate,
        long TotalCandles);

    internal sealed class Handler(IBacktestDbContext context) : IQueryHandler<Request, Result<List<AssetDto>>>
    {
        public async Task<Result<List<AssetDto>>> Handle(Request request, CancellationToken cancellationToken)
        {
            List<AssetDto> assets = await context.BacktestAssets
                .AsNoTracking()
                .Where(a => a.SyncStatus == AssetSyncStatus.Ready)
                .OrderBy(a => a.Category)
                .ThenBy(a => a.Symbol)
                .Select(a => new AssetDto(
                    a.Id,
                    a.DisplayName,
                    a.Symbol,
                    a.Category,
                    a.DataStartDate,
                    a.DataEndDate,
                    a.TotalCandles))
                .ToListAsync(cancellationToken);

            return Result<List<AssetDto>>.Success(assets);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet(ApiGroup.V1.Assets, async (ISender sender) =>
            {
                Result<List<AssetDto>> result = await sender.Send(new Request());
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithTags(Tags.BacktestAssets)
            .WithDescription("Get a list of available assets for backtesting.")
            .Produces<Result<List<AssetDto>>>();
        }
    }
}
