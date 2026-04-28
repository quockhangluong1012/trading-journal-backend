using TradingJournal.Modules.Scanner.Dto;

namespace TradingJournal.Modules.Scanner.Features.V1.Watchlists;

public sealed class AddWatchlistAsset
{
    public record Command() : ICommand<Result<WatchlistAssetDto>>
    {
        public int UserId { get; set; }
        public int WatchlistId { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.WatchlistId).GreaterThan(0);
            RuleFor(x => x.Symbol).NotEmpty().MaximumLength(30);
            RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        }
    }

    internal sealed class Handler(IScannerDbContext context)
        : ICommandHandler<Command, Result<WatchlistAssetDto>>
    {
        public async Task<Result<WatchlistAssetDto>> Handle(Command request, CancellationToken cancellationToken)
        {
            Watchlist? watchlist = await context.Watchlists
                .FirstOrDefaultAsync(w => w.Id == request.WatchlistId && w.UserId == request.UserId, cancellationToken);

            if (watchlist is null)
                return Result<WatchlistAssetDto>.Failure(new Error("WatchlistNotFound", "Watchlist not found."));

            // Check for duplicate symbol within the same watchlist
            bool exists = await context.WatchlistAssets
                .AnyAsync(a => a.WatchlistId == request.WatchlistId && a.Symbol == request.Symbol.ToUpperInvariant(), cancellationToken);

            if (exists)
                return Result<WatchlistAssetDto>.Failure(new Error("DuplicateAsset", $"Asset '{request.Symbol}' already exists in this watchlist."));

            var asset = new WatchlistAsset
            {
                Id = default!,
                WatchlistId = request.WatchlistId,
                Symbol = request.Symbol.ToUpperInvariant(),
                DisplayName = request.DisplayName
            };

            context.WatchlistAssets.Add(asset);
            await context.SaveChangesAsync(cancellationToken);

            var dto = new WatchlistAssetDto(asset.Id, asset.Symbol, asset.DisplayName, new List<string>());
            return Result<WatchlistAssetDto>.Success(dto);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Watchlists);

            group.MapPost("/{watchlistId:int}/assets",
                async (int watchlistId, ClaimsPrincipal user, ISender sender, CreateWatchlistAssetRequest body) =>
                {
                    var command = new Command
                    {
                        UserId = user.GetCurrentUserId(),
                        WatchlistId = watchlistId,
                        Symbol = body.Symbol,
                        DisplayName = body.DisplayName
                    };

                    Result<WatchlistAssetDto> result = await sender.Send(command);
                    return result.IsSuccess
                        ? Results.Created($"/api/v1/scanner/watchlists/{watchlistId}/assets/{result.Value.Id}", result)
                        : Results.BadRequest(result);
                })
            .Produces<Result<WatchlistAssetDto>>(StatusCodes.Status201Created)
            .WithSummary("Add an asset to an existing watchlist.")
            .WithTags(Tags.Watchlists)
            .RequireAuthorization();
        }
    }
}
