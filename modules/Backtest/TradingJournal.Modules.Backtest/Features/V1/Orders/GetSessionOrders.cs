using TradingJournal.Modules.Backtest.Dto;

namespace TradingJournal.Modules.Backtest.Features.V1.Orders;

public sealed class GetSessionOrders
{
    public record Request(int SessionId) : IQuery<Result<List<OrderDto>>>
    {
        public int UserId { get; set; }
    }

    internal sealed class Handler(IBacktestDbContext context) : IQueryHandler<Request, Result<List<OrderDto>>>
    {
        public async Task<Result<List<OrderDto>>> Handle(Request request, CancellationToken cancellationToken)
        {
            List<OrderDto> orders = await context.BacktestOrders
                .Where(o => o.SessionId == request.SessionId && o.Session.CreatedBy == request.UserId)
                .OrderByDescending(o => o.OrderedAt)
                .Select(o => new OrderDto(
                    o.Id,
                    o.OrderType.ToString(),
                    o.Side.ToString(),
                    o.Status.ToString(),
                    o.EntryPrice,
                    o.FilledPrice,
                    o.PositionSize,
                    o.StopLoss,
                    o.TakeProfit,
                    o.ExitPrice,
                    o.Pnl,
                    o.OrderedAt,
                    o.FilledAt,
                    o.ClosedAt))
                .ToListAsync(cancellationToken);

            return Result<List<OrderDto>>.Success(orders);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Orders);

            group.MapGet("/session/{sessionId:int}", async (int sessionId, ClaimsPrincipal user, ISender sender) =>
            {
                Result<List<OrderDto>> result = await sender.Send(new Request(sessionId) with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<List<OrderDto>>>(StatusCodes.Status200OK)
            .WithSummary("Get all orders for a backtest session.")
            .WithTags(Tags.BacktestOrders)
            .RequireAuthorization();
        }
    }
}
