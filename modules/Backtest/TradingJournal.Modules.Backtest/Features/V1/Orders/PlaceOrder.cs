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

            bool isMarket = request.OrderType == BacktestOrderType.Market;

            // OHLC data is BID. The client sends the price it is displaying (the current bid),
            // so apply the session spread server-side: a long fills at the ASK (bid + spread),
            // a short fills at the BID. This keeps market entries consistent with how the
            // matching engine prices SL/TP exits — previously market orders skipped the spread
            // entirely, handing every long a free half-spread on entry.
            decimal marketFillPrice = isMarket && request.Side == BacktestOrderSide.Long
                ? request.EntryPrice + session.Spread
                : request.EntryPrice;

            // Validate SL/TP sit on the correct side of the entry, so a mistyped level can't
            // open a position that liquidates itself on the very next candle.
            decimal entryReference = isMarket ? marketFillPrice : request.EntryPrice;

            if (request.StopLoss is decimal sl)
            {
                bool slValid = request.Side == BacktestOrderSide.Long ? sl < entryReference : sl > entryReference;
                if (!slValid)
                    return Result<OrderDto>.Failure(Error.Create(request.Side == BacktestOrderSide.Long
                        ? "Stop loss must be below the entry price for a long position."
                        : "Stop loss must be above the entry price for a short position."));
            }

            if (request.TakeProfit is decimal tp)
            {
                bool tpValid = request.Side == BacktestOrderSide.Long ? tp > entryReference : tp < entryReference;
                if (!tpValid)
                    return Result<OrderDto>.Failure(Error.Create(request.Side == BacktestOrderSide.Long
                        ? "Take profit must be above the entry price for a long position."
                        : "Take profit must be below the entry price for a short position."));
            }

            BacktestOrderStatus initialStatus = isMarket
                ? BacktestOrderStatus.Active
                : BacktestOrderStatus.Pending;

            BacktestOrder order = new()
            {
                Id = 0,
                SessionId = request.SessionId,
                OrderType = request.OrderType,
                Side = request.Side,
                Status = initialStatus,
                EntryPrice = isMarket ? marketFillPrice : request.EntryPrice,
                FilledPrice = isMarket ? marketFillPrice : null,
                PositionSize = request.PositionSize,
                StopLoss = request.StopLoss,
                TakeProfit = request.TakeProfit,
                OrderedAt = session.CurrentTimestamp,
                FilledAt = isMarket ? session.CurrentTimestamp : null
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
