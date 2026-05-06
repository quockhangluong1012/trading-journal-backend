using TradingJournal.Modules.Trades.Services;

namespace TradingJournal.Modules.Trades.Features.V1.Trade;

public sealed class DeleteTrade
{
    public sealed class Request : ICommand<Result<int>>
    {
        public int Id { get; set; }
        public int UserId { get; set; }
    }
    
    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Id)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Trade ID must be greater than 0.");
        }
    }

    public sealed class Handler(ITradeDbContext tradeDbContext, IScreenshotService screenshotService, ICacheRepository cacheRepository) : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            Domain.TradeHistory? trade = await tradeDbContext.TradeHistories
                .Include(x => x.TradeScreenShots)
                .FirstOrDefaultAsync(x => x.Id == request.Id && x.CreatedBy == request.UserId, cancellationToken);

            if (trade == null)
            {
                return Result<int>.Failure(Error.NotFound);
            }

            // Delete physical screenshot files from disk
            foreach (var screenshot in trade.TradeScreenShots)
            {
                await screenshotService.DeleteScreenshotAsync(screenshot.Url, cancellationToken);
            }

            // Soft-delete: mark as disabled instead of hard-removing from the database.
            // The global query filter on EntityBase<int>.IsDisabled will exclude it from queries.
            trade.IsDisabled = true;

            await tradeDbContext.SaveChangesAsync(cancellationToken);
            await cacheRepository.RemoveCache(CacheKeys.TradesForUser(request.UserId), cancellationToken);

            return Result<int>.Success(trade.Id);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradeHistory);

            group.MapDelete("/{id}", async ([FromRoute] int id, ClaimsPrincipal user, ISender sender) => {
                Result<int> result = await sender.Send(new Request { Id = id, UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) 
                    : Results.BadRequest(result);
            })
            .Produces<Result<int>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Delete a trade history by ID.")
            .WithDescription("Soft-deletes a trade history by its ID.") 
            .WithTags(Tags.TradeHistory)
            .RequireAuthorization();
        }
    }
}