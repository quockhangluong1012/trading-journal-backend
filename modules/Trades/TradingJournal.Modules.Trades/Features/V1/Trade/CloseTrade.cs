using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Messaging.Shared.Events;

namespace TradingJournal.Modules.Trades.Features.V1.Trade;

public sealed class CloseTrade
{
    public sealed record Request(int TradeId, decimal ExitPrice, decimal PnL, string? TradingResult, bool? HitStopLoss, int UserId = 0) : ICommand<Result<bool>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.TradeId).GreaterThan(0)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Trade ID must be greater than 0.");

            RuleFor(r => r.ExitPrice).GreaterThan(0)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Exit price must be greater than 0.");
        }
    }

    public sealed class Handler(ITradeDbContext tradeDbContext,
        IEventBus eventBus) : ICommandHandler<Request, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Request request, CancellationToken cancellationToken)
        {
            TradeHistory? tradeHistory = await tradeDbContext.TradeHistories
                .FirstOrDefaultAsync(th => th.Id == request.TradeId && th.CreatedBy == request.UserId, cancellationToken);

            if (tradeHistory == null)
            {
                return Result<bool>.Failure(Error.NotFound);
            }

            tradeHistory.ExitPrice = request.ExitPrice;
            tradeHistory.Pnl = request.PnL;
            tradeHistory.TradingResult = request.TradingResult;
            tradeHistory.HitStopLoss = request.HitStopLoss;
            tradeHistory.ClosedDate = DateTime.UtcNow;
            tradeHistory.Status = TradeStatus.Closed;

            await tradeDbContext.SaveChangesAsync(cancellationToken);

            await eventBus.PublishAsync(new SummarizeTradingOrderEvent(Guid.NewGuid(), DateTime.UtcNow, tradeHistory.Id), cancellationToken);

            return Result<bool>.Success(true);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradeHistory);

            group.MapPost("/close", async ([FromBody] Request request, ClaimsPrincipal user, ISender sender) =>
            {
                Result<bool> result = await sender.Send(request with { UserId = user.GetCurrentUserId() });
                return result;
            })
            .Produces<Result<bool>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Close a trade.")
            .WithDescription("Closes a trade by setting its exit price, calculating PnL, and updating status to Closed.")
            .WithTags(Tags.TradeHistory)
            .RequireAuthorization();
        }
    }
}
