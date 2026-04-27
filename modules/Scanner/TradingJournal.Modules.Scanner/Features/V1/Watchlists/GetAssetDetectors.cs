using TradingJournal.Modules.Scanner.Dto;

namespace TradingJournal.Modules.Scanner.Features.V1.Watchlists;

public sealed class GetAssetDetectors
{
    public record Request() : IQuery<Result<WatchlistAssetDto>>
    {
        public int UserId { get; set; }
        public int WatchlistId { get; set; }
        public int AssetId { get; set; }
    }

    internal sealed class Handler(IScannerDbContext context)
        : IQueryHandler<Request, Result<WatchlistAssetDto>>
    {
        public async Task<Result<WatchlistAssetDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            WatchlistAsset? asset = await context.WatchlistAssets
                .Include(a => a.Watchlist)
                .Include(a => a.EnabledDetectors.Where(d => !d.IsDisabled))
                .FirstOrDefaultAsync(a =>
                    a.Id == request.AssetId &&
                    a.WatchlistId == request.WatchlistId &&
                    a.Watchlist.UserId == request.UserId &&
                    !a.IsDisabled, cancellationToken);

            if (asset is null)
                return Result<WatchlistAssetDto>.Failure(new Error("AssetNotFound", "Watchlist asset not found."));

            var dto = new WatchlistAssetDto(
                asset.Id,
                asset.Symbol,
                asset.DisplayName,
                asset.EnabledDetectors
                    .Where(d => d.IsEnabled)
                    .Select(d => d.PatternType.ToString())
                    .ToList());

            return Result<WatchlistAssetDto>.Success(dto);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Watchlists);

            group.MapGet("/{watchlistId:int}/assets/{assetId:int}/detectors",
                async (int watchlistId, int assetId, ClaimsPrincipal user, ISender sender) =>
                {
                    Result<WatchlistAssetDto> result = await sender.Send(
                        new Request
                        {
                            UserId = user.GetCurrentUserId(),
                            WatchlistId = watchlistId,
                            AssetId = assetId
                        });

                    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
                })
            .Produces<Result<WatchlistAssetDto>>(StatusCodes.Status200OK)
            .WithSummary("Get enabled detectors for a specific watchlist asset.")
            .WithTags(Tags.Watchlists)
            .RequireAuthorization();
        }
    }
}
