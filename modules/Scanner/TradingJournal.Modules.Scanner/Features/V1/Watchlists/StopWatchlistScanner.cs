using TradingJournal.Modules.Scanner.Dto;

namespace TradingJournal.Modules.Scanner.Features.V1.Watchlists;

public sealed class StopWatchlistScanner
{
    public record Command() : ICommand<Result<WatchlistDto>>
    {
        public int UserId { get; set; }
        public int WatchlistId { get; set; }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.WatchlistId).GreaterThan(0);
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

            watchlist.IsScannerRunning = false;
            await context.SaveChangesAsync(cancellationToken);

            var dto = new WatchlistDto(
                watchlist.Id,
                watchlist.Name,
                watchlist.IsActive,
                watchlist.IsScannerRunning,
                watchlist.CreatedDate,
                watchlist.Assets.Select(a => new WatchlistAssetDto(
                    a.Id, a.Symbol, a.DisplayName, new List<string>())).ToList());

            return Result<WatchlistDto>.Success(dto);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Watchlists);

            group.MapPost("/{id:int}/scanner/stop", async (int id, ClaimsPrincipal user, ISender sender) =>
            {
                Result<WatchlistDto> result = await sender.Send(
                    new Command { UserId = user.GetCurrentUserId(), WatchlistId = id });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<WatchlistDto>>(StatusCodes.Status200OK)
            .WithSummary("Stop the scanner engine for a specific watchlist.")
            .WithTags(Tags.Watchlists)
            .RequireAuthorization();
        }
    }
}
