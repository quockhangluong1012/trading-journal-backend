using System.Text.Json.Serialization;
using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Modules.Backtest.Dto;

namespace TradingJournal.Modules.Backtest.Features.V1.Orders;

public sealed class UpdateOrder
{
    public record Request(
        int OrderId,
        decimal? EntryPrice,
        decimal? PositionSize,
        decimal? StopLoss,
        decimal? TakeProfit) : ICommand<Result<OrderDto>>
    {
        [JsonIgnore]
        public int UserId { get; set; }
    }

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("Order ID is required.");
            RuleFor(x => x.PositionSize).GreaterThan(0).When(x => x.PositionSize.HasValue).WithMessage("Position size must be greater than 0.");
            RuleFor(x => x.EntryPrice).GreaterThan(0).When(x => x.EntryPrice.HasValue).WithMessage("Entry price must be greater than 0.");
        }
    }

    internal sealed class Handler(IBacktestDbContext context) : ICommandHandler<Request, Result<OrderDto>>
    {
        public async Task<Result<OrderDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            BacktestOrder? order = await context.BacktestOrders
                .Include(o => o.Session)
                .FirstOrDefaultAsync(o => o.Id == request.OrderId && o.Session!.CreatedBy == request.UserId, cancellationToken);

            if (order is null)
                return Result<OrderDto>.Failure(Error.Create("Order not found."));

            if (order.Status == BacktestOrderStatus.Closed || order.Status == BacktestOrderStatus.Cancelled)
                return Result<OrderDto>.Failure(Error.Create("Cannot update a closed or cancelled order."));

            if (order.Status == BacktestOrderStatus.Active)
            {
                // For active orders, we can only update SL and TP
                order.StopLoss = request.StopLoss;
                order.TakeProfit = request.TakeProfit;
            }
            else if (order.Status == BacktestOrderStatus.Pending)
            {
                // For pending orders, we can update everything
                order.StopLoss = request.StopLoss;
                order.TakeProfit = request.TakeProfit;
                if (request.EntryPrice.HasValue) order.EntryPrice = request.EntryPrice.Value;
                if (request.PositionSize.HasValue) order.PositionSize = request.PositionSize.Value;
            }

            await context.SaveChangesAsync(cancellationToken);

            OrderDto dto = new(
                order.Id,
                order.OrderType.ToString(),
                order.Side.ToString(),
                order.Status.ToString(),
                order.EntryPrice,
                order.FilledPrice,
                order.PositionSize,
                order.StopLoss,
                order.TakeProfit,
                order.ExitPrice,
                order.Pnl,
                order.OrderedAt,
                order.FilledAt,
                order.ClosedAt);

            return Result<OrderDto>.Success(dto);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Orders);

            group.MapPut("/{orderId:int}", async (int orderId, [FromBody] Request body, ClaimsPrincipal user, ISender sender) =>
            {
                var request = body with { OrderId = orderId };
                Result<OrderDto> result = await sender.Send(request with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<OrderDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Update a pending limit order or SL/TP bounds of an active position.")
            .WithTags(Tags.BacktestOrders)
            .RequireAuthorization();
        }
    }
}
