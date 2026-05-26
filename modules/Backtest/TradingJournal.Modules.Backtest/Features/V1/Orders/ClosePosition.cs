namespace TradingJournal.Modules.Backtest.Features.V1.Orders;

public sealed class ClosePosition
{
    public record Request(int OrderId, decimal ExitPrice) : ICommand<Result>
    {
        public int UserId { get; set; }
    }

    internal sealed class Handler(IBacktestDbContext context) : ICommandHandler<Request, Result>
    {
        public async Task<Result> Handle(Request request, CancellationToken cancellationToken)
        {
            BacktestOrder? order = await context.BacktestOrders
                .Include(o => o.Session)
                .FirstOrDefaultAsync(o => o.Id == request.OrderId
                                          && o.Session.CreatedBy == request.UserId, cancellationToken);

            if (order is null)
                return Result.Failure(Error.Create("Order not found."));

            if (order.Status != BacktestOrderStatus.Active)
                return Result.Failure(Error.Create("Only active orders can be closed."));

            DateTime closeTimestamp = order.Session.CurrentTimestamp;

            order.Status = BacktestOrderStatus.Closed;
            order.ExitPrice = request.ExitPrice;
            order.ClosedAt = closeTimestamp;

            decimal entryPrice = order.FilledPrice ?? order.EntryPrice;
            
            order.Pnl = order.Side switch
            {
                BacktestOrderSide.Long => (order.ExitPrice.Value - entryPrice) * order.PositionSize,
                BacktestOrderSide.Short => (entryPrice - order.ExitPrice.Value) * order.PositionSize,
                _ => 0m
            };

            order.Session.CurrentBalance += order.Pnl.Value;

            BacktestTradeResult tradeResult = new()
            {
                Id = 0,
                SessionId = order.SessionId,
                OrderId = order.Id,
                Side = order.Side,
                EntryPrice = entryPrice,
                ExitPrice = order.ExitPrice.Value,
                PositionSize = order.PositionSize,
                Pnl = order.Pnl.Value,
                BalanceAfter = order.Session.CurrentBalance,
                EntryTime = order.FilledAt ?? order.OrderedAt,
                ExitTime = closeTimestamp,
                ExitReason = "Manual"
            };

            context.BacktestTradeResults.Add(tradeResult);

            await context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Orders);

            group.MapPost("/{orderId:int}/close", async (int orderId, decimal exitPrice, ClaimsPrincipal user, ISender sender) =>
            {
                Result result = await sender.Send(new Request(orderId, exitPrice) with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result);
            })
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Manually close an active position.")
            .WithTags(Tags.BacktestOrders)
            .RequireAuthorization();
        }
    }
}
