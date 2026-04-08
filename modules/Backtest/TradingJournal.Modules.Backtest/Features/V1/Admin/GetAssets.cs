namespace TradingJournal.Modules.Backtest.Features.V1.Admin;

/// <summary>
/// Admin endpoint to list all registered backtest assets with their sync status.
/// </summary>
public static class GetAssets
{
    public sealed record Request() : IQuery<Result<List<AssetDto>>>;

    public sealed record AssetDto(
        int Id,
        string DisplayName,
        string Symbol,
        string Category,
        string DataProvider,
        string SyncStatus,
        DateTime DataStartDate,
        DateTime? DataEndDate,
        DateTime? LastSyncedDate,
        long TotalCandles,
        string? LastError,
        DateTime CreatedDate);

    internal sealed class Handler(IBacktestDbContext context) : IQueryHandler<Request, Result<List<AssetDto>>>
    {
        public async Task<Result<List<AssetDto>>> Handle(Request request, CancellationToken cancellationToken)
        {
            List<AssetDto> assets = await context.BacktestAssets
                .OrderBy(a => a.Category)
                .ThenBy(a => a.DisplayName)
                .Select(a => new AssetDto(
                    a.Id,
                    a.DisplayName,
                    a.Symbol,
                    a.Category,
                    a.DataProvider,
                    a.SyncStatus.ToString(),
                    a.DataStartDate,
                    a.DataEndDate,
                    a.LastSyncedDate,
                    a.TotalCandles,
                    a.LastError,
                    a.CreatedDate))
                .ToListAsync(cancellationToken);

            return Result<List<AssetDto>>.Success(assets);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet(ApiGroup.V1.Admin, async (ISender sender) =>
            {
                Result<List<AssetDto>> result = await sender.Send(new Request());
                return Results.Ok(result);
            })
            .WithTags(Tags.BacktestAdmin)
            .WithDescription("List all registered backtest assets with sync status.")
            .Produces<Result<List<AssetDto>>>()
            .RequireAuthorization();
        }
    }
}
