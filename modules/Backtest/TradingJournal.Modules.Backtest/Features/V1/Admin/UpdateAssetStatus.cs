namespace TradingJournal.Modules.Backtest.Features.V1.Admin;

/// <summary>
/// Admin endpoint to update the sync status of an asset.
/// Use cases:
///   - Retry a failed sync: set status to Syncing
///   - Pause an active sync: set status to Paused
///   - Resume a paused sync: set status to Syncing
/// </summary>
public static class UpdateAssetStatus
{
    public sealed record Request(int Id, string Status) : ICommand<Result>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.Status).Must(s =>
                s is "Pending" or "Syncing" or "Paused")
                .WithMessage("Status must be one of: Pending, Syncing, Paused");
        }
    }

    internal sealed class Handler(IBacktestDbContext context) : ICommandHandler<Request, Result>
    {
        public async Task<Result> Handle(Request request, CancellationToken cancellationToken)
        {
            BacktestAsset? asset = await context.BacktestAssets
                .FindAsync([request.Id], cancellationToken);

            if (asset is null)
                return Result.Failure(new Error("Asset.NotFound", "Asset not found."));

            asset.SyncStatus = Enum.Parse<AssetSyncStatus>(request.Status);
            asset.LastError = null; // Clear error on retry
            await context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPatch(ApiGroup.V1.Admin + "/{id:int}/status", async (
                int id,
                UpdateAssetStatus.Request request,
                ISender sender) =>
            {
                // Ensure the ID from the route matches the request
                Result result = await sender.Send(request with { Id = id });
                return result.IsSuccess
                    ? Results.Ok(result)
                    : Results.NotFound(result);
            })
            .WithTags(Tags.BacktestAdmin)
            .WithDescription("Update sync status of an asset (retry, pause, resume).")
            .Produces<Result>()
            .Produces<Result>(StatusCodes.Status404NotFound)
            .RequireAuthorization();
        }
    }
}
