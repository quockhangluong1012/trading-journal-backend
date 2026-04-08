namespace TradingJournal.Modules.Backtest.Features.V1.Admin;

/// <summary>
/// Admin endpoint to delete an asset and all its associated M1 candle data.
/// </summary>
public static class DeleteAsset
{
    public sealed record Request(int Id) : ICommand<Result>;

    internal sealed class Handler(IBacktestDbContext context) : ICommandHandler<Request, Result>
    {
        public async Task<Result> Handle(Request request, CancellationToken cancellationToken)
        {
            BacktestAsset? asset = await context.BacktestAssets
                .FindAsync([request.Id], cancellationToken);

            if (asset is null)
                return Result.Failure(new Error("Asset.NotFound", "Asset not found."));

            // Delete all associated candles
            await context.OhlcvCandles
                .Where(c => c.Asset == asset.Symbol)
                .ExecuteDeleteAsync(cancellationToken);

            context.BacktestAssets.Remove(asset);
            await context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapDelete(ApiGroup.V1.Admin + "/{id:int}", async (int id, ISender sender) =>
            {
                Result result = await sender.Send(new Request(id));
                return result.IsSuccess
                    ? Results.NoContent()
                    : Results.NotFound(result);
            })
            .WithTags(Tags.BacktestAdmin)
            .WithDescription("Delete an asset and all its candle data.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization();
        }
    }
}
