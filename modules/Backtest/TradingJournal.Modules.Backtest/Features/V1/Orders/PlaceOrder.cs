using TradingJournal.Modules.Backtest.Dto;

namespace TradingJournal.Modules.Backtest.Features.V1.Orders;

public sealed class PlaceOrder
{
    public record Request(
        int SessionId,
        BacktestOrderType OrderType,
        BacktestOrderSide Side,
        decimal EntryPrice,
        decimal PositionSize,
        decimal? StopLoss,
        decimal? TakeProfit) : ICommand<Result<OrderDto>>
    {
        public int UserId { get; set; }
    }

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.SessionId)
                .GreaterThan(0).WithMessage("Session ID is required.");

            RuleFor(x => x.PositionSize)
                .GreaterThan(0).WithMessage("Position size must be greater than 0.");

            RuleFor(x => x.EntryPrice)
                .GreaterThan(0).WithMessage("Entry price must be greater than 0.");

            RuleFor(x => x.OrderType)
                .Must(Enum.IsDefined).WithMessage("Invalid order type.");

            RuleFor(x => x.Side)
                .Must(Enum.IsDefined).WithMessage("Invalid order side.");
        }
    }

    internal sealed class Handler(IBacktestDbContext context) : ICommandHandler<Request, Result<OrderDto>>
    {
        public async Task<Result<OrderDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            BacktestSession? session = await context.BacktestSessions
                .FirstOrDefaultAsync(s => s.Id == request.SessionId
                                          && s.CreatedBy == request.UserId
                                          && s.Status == BacktestSessionStatus.InProgress, cancellationToken);

            if (session is null)
                return Result<OrderDto>.Failure(Error.Create("Active session not found."));

            // For market orders, execute immediately at the current candle's close price
            // The entry price for market orders should be the close of the current candle
            BacktestOrderStatus initialStatus = request.OrderType == BacktestOrderType.Market
                ? BacktestOrderStatus.Active
                : BacktestOrderStatus.Pending;

            BacktestOrder order = new()
            {
                Id = 0,
                SessionId = request.SessionId,
                OrderType = request.OrderType,
                Side = request.Side,
                Status = initialStatus,
                EntryPrice = request.EntryPrice,
                FilledPrice = request.OrderType == BacktestOrderType.Market ? request.EntryPrice : null,
                PositionSize = request.PositionSize,
                StopLoss = request.StopLoss,
                TakeProfit = request.TakeProfit,
                OrderedAt = session.CurrentTimestamp,
                FilledAt = request.OrderType == BacktestOrderType.Market ? session.CurrentTimestamp : null
            };

            await context.BacktestOrders.AddAsync(order, cancellationToken);
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

            group.MapPost("/", async ([FromBody] Request request, ClaimsPrincipal user, ISender sender) =>
            {
                Result<OrderDto> result = await sender.Send(request with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess
                    ? Results.Created($"{ApiGroup.V1.Orders}/{result.Value.Id}", result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<OrderDto>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Place a new order (market or limit) in a backtest session.")
            .WithTags(Tags.BacktestOrders)
            .RequireAuthorization();
        }
    }
}
