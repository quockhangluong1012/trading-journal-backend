namespace TradingJournal.Modules.Scanner.Features.V1.Watchlists;

public sealed class RemoveWatchlistAsset
{
    public record Command() : ICommand<Result>
    {
        public int UserId { get; set; }
        public int WatchlistId { get; set; }
        public int AssetId { get; set; }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.WatchlistId).GreaterThan(0);
            RuleFor(x => x.AssetId).GreaterThan(0);
        }
    }

    internal sealed class Handler(IScannerDbContext context)
        : ICommandHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            // Verify the watchlist belongs to the user
            bool watchlistExists = await context.Watchlists
                .AnyAsync(w => w.Id == request.WatchlistId && w.UserId == request.UserId, cancellationToken);

            if (!watchlistExists)
                return Result.Failure(new Error("WatchlistNotFound", "Watchlist not found."));

            WatchlistAsset? asset = await context.WatchlistAssets
                .FirstOrDefaultAsync(a => a.Id == request.AssetId && a.WatchlistId == request.WatchlistId, cancellationToken);

            if (asset is null)
                return Result.Failure(new Error("AssetNotFound", "Asset not found in this watchlist."));

            context.WatchlistAssets.Remove(asset);
            await context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Watchlists);

            group.MapDelete("/{watchlistId:int}/assets/{assetId:int}",
                async (int watchlistId, int assetId, ClaimsPrincipal user, ISender sender) =>
                {
                    var command = new Command
                    {
                        UserId = user.GetCurrentUserId(),
                        WatchlistId = watchlistId,
                        AssetId = assetId
                    };

                    Result result = await sender.Send(command);
                    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result);
                })
            .Produces(StatusCodes.Status204NoContent)
            .WithSummary("Remove an asset from a watchlist.")
            .WithTags(Tags.Watchlists)
            .RequireAuthorization();
        }
    }
}
