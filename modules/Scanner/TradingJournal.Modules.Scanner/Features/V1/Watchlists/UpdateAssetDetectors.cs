using TradingJournal.Modules.Scanner.Dto;

namespace TradingJournal.Modules.Scanner.Features.V1.Watchlists;

public sealed class UpdateAssetDetectors
{
    public record Command() : ICommand<Result<WatchlistAssetDto>>
    {
        public int UserId { get; set; }
        public int WatchlistId { get; set; }
        public int AssetId { get; set; }
        public List<string> EnabledPatterns { get; set; } = [];
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
        : ICommandHandler<Command, Result<WatchlistAssetDto>>
    {
        public async Task<Result<WatchlistAssetDto>> Handle(Command request, CancellationToken cancellationToken)
        {
            // Verify the asset belongs to the user's watchlist
            WatchlistAsset? asset = await context.WatchlistAssets
                .Include(a => a.Watchlist)
                .Include(a => a.EnabledDetectors)
                .FirstOrDefaultAsync(a =>
                    a.Id == request.AssetId &&
                    a.WatchlistId == request.WatchlistId &&
                    a.Watchlist.UserId == request.UserId &&
                    !a.IsDisabled, cancellationToken);

            if (asset is null)
                return Result<WatchlistAssetDto>.Failure(new Error("AssetNotFound", "Watchlist asset not found."));

            // Parse the requested pattern types
            var requestedPatterns = new List<IctPatternType>();
            foreach (string patternStr in request.EnabledPatterns)
            {
                if (Enum.TryParse<IctPatternType>(patternStr, ignoreCase: true, out var patternType))
                {
                    requestedPatterns.Add(patternType);
                }
            }

            // Remove existing detector config
            context.WatchlistAssetDetectors.RemoveRange(asset.EnabledDetectors);

            // Add new detector config
            asset.EnabledDetectors = requestedPatterns.Select(p => new WatchlistAssetDetector
            {
                Id = default!,
                WatchlistAssetId = asset.Id,
                PatternType = p,
                IsEnabled = true
            }).ToList();

            await context.SaveChangesAsync(cancellationToken);

            var dto = new WatchlistAssetDto(
                asset.Id,
                asset.Symbol,
                asset.DisplayName,
                asset.EnabledDetectors.Select(d => d.PatternType.ToString()).ToList());

            return Result<WatchlistAssetDto>.Success(dto);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Watchlists);

            group.MapPut("/{watchlistId:int}/assets/{assetId:int}/detectors",
                async (int watchlistId, int assetId, ClaimsPrincipal user, ISender sender, UpdateAssetDetectorsRequest body) =>
                {
                    var command = new Command
                    {
                        UserId = user.GetCurrentUserId(),
                        WatchlistId = watchlistId,
                        AssetId = assetId,
                        EnabledPatterns = body.EnabledPatterns
                    };

                    Result<WatchlistAssetDto> result = await sender.Send(command);
                    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
                })
            .Produces<Result<WatchlistAssetDto>>(StatusCodes.Status200OK)
            .WithSummary("Set enabled detectors for a specific watchlist asset.")
            .WithTags(Tags.Watchlists)
            .RequireAuthorization();
        }
    }
}
