namespace TradingJournal.Modules.Scanner.Features.V1.Watchlists;

public sealed class DeleteWatchlist
{
    public record Command() : ICommand<Result<bool>>
    {
        public int UserId { get; set; }
        public int WatchlistId { get; set; }
    }

    internal sealed class Handler(IScannerDbContext context)
        : ICommandHandler<Command, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Command request, CancellationToken cancellationToken)
        {
            Watchlist? watchlist = await context.Watchlists
                .FirstOrDefaultAsync(w => w.Id == request.WatchlistId && w.UserId == request.UserId, cancellationToken);

            if (watchlist is null)
                return Result<bool>.Failure(new Error("WatchlistNotFound", "Watchlist not found."));

            // Soft delete
            watchlist.IsDisabled = true;

            await context.SaveChangesAsync(cancellationToken);

            return Result<bool>.Success(true);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Watchlists);

            group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal user, ISender sender) =>
            {
                Result<bool> result = await sender.Send(new Command
                {
                    UserId = user.GetCurrentUserId(),
                    WatchlistId = id
                });

                return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result);
            })
            .Produces(StatusCodes.Status204NoContent)
            .WithSummary("Delete a watchlist (soft-delete).")
            .WithTags(Tags.Watchlists)
            .RequireAuthorization();
        }
    }
}
