namespace TradingJournal.Modules.Backtest.Features.V1.Orders;

public sealed class CancelOrder
{
    public record Request(int OrderId) : ICommand<Result>
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

            if (order.Status != BacktestOrderStatus.Pending)
                return Result.Failure(Error.Create("Only pending orders can be cancelled."));

            order.Status = BacktestOrderStatus.Cancelled;
            await context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Orders);

            group.MapDelete("/{orderId:int}", async (int orderId, ISender sender) =>
            {
                Result result = await sender.Send(new Request(orderId));

                return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result);
            })
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Cancel a pending order.")
            .WithTags(Tags.BacktestOrders)
            .RequireAuthorization();
        }
    }
}
