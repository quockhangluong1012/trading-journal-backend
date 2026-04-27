using TradingJournal.Modules.Scanner.Dto;

namespace TradingJournal.Modules.Scanner.Features.V1.Watchlists;

public sealed class UpdateWatchlist
{
    public record Command() : ICommand<Result<WatchlistDto>>
    {
        public int UserId { get; set; }
        public int WatchlistId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public List<CreateWatchlistAssetRequest> Assets { get; set; } = [];
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.WatchlistId).GreaterThan(0);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleForEach(x => x.Assets).ChildRules(a =>
            {
                a.RuleFor(x => x.Symbol).NotEmpty().MaximumLength(30);
                a.RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
            });
        }
    }

    internal sealed class Handler(IScannerDbContext context)
        : ICommandHandler<Command, Result<WatchlistDto>>
    {
        public async Task<Result<WatchlistDto>> Handle(Command request, CancellationToken cancellationToken)
        {
            Watchlist? watchlist = await context.Watchlists
                .Include(w => w.Assets)
                .FirstOrDefaultAsync(w => w.Id == request.WatchlistId && w.UserId == request.UserId, cancellationToken);

            if (watchlist is null)
                return Result<WatchlistDto>.Failure(new Error("WatchlistNotFound", "Watchlist not found."));

            watchlist.Name = request.Name;
            watchlist.IsActive = request.IsActive;

            // Replace assets: remove old, add new
            context.WatchlistAssets.RemoveRange(watchlist.Assets);

            watchlist.Assets = request.Assets.Select(a => new WatchlistAsset
            {
                Id = default!,
                WatchlistId = watchlist.Id,
                Symbol = a.Symbol.ToUpperInvariant(),
                DisplayName = a.DisplayName
            }).ToList();

            await context.SaveChangesAsync(cancellationToken);

            var dto = new WatchlistDto(
                watchlist.Id,
                watchlist.Name,
                watchlist.IsActive,
                watchlist.CreatedDate,
                watchlist.Assets.Select(a => new WatchlistAssetDto(a.Id, a.Symbol, a.DisplayName, new List<string>())).ToList());

            return Result<WatchlistDto>.Success(dto);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Watchlists);

            group.MapPut("/{id:int}", async (int id, ClaimsPrincipal user, ISender sender, UpdateWatchlistRequest body) =>
            {
                var command = new Command
                {
                    UserId = user.GetCurrentUserId(),
                    WatchlistId = id,
                    Name = body.Name,
                    IsActive = body.IsActive,
                    Assets = body.Assets
                };

                Result<WatchlistDto> result = await sender.Send(command);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<WatchlistDto>>(StatusCodes.Status200OK)
            .WithSummary("Update a watchlist and its assets.")
            .WithTags(Tags.Watchlists)
            .RequireAuthorization();
        }
    }
}
