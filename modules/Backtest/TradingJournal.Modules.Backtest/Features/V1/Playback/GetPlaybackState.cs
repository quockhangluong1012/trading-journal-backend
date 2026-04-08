using TradingJournal.Modules.Backtest.Dto;

namespace TradingJournal.Modules.Backtest.Features.V1.Playback;

public sealed class GetPlaybackState
{
    public record Request(int SessionId) : IQuery<Result<PlaybackStateDto>>
    {
        public int UserId { get; set; }
    }

    internal sealed class Handler(IBacktestDbContext context) : IQueryHandler<Request, Result<PlaybackStateDto>>
    {
        public async Task<Result<PlaybackStateDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            BacktestSession? session = await context.BacktestSessions
                .FirstOrDefaultAsync(s => s.Id == request.SessionId
                                          && s.CreatedBy == request.UserId, cancellationToken);

            if (session is null)
                return Result<PlaybackStateDto>.Failure(Error.Create("Session not found."));

            List<OrderDto> pendingOrders = await context.BacktestOrders
                .Where(o => o.SessionId == request.SessionId && o.Status == BacktestOrderStatus.Pending)
                .Select(o => new OrderDto(
                    o.Id, o.OrderType.ToString(), o.Side.ToString(), o.Status.ToString(),
                    o.EntryPrice, o.FilledPrice, o.PositionSize, o.StopLoss, o.TakeProfit,
                    o.ExitPrice, o.Pnl, o.OrderedAt, o.FilledAt, o.ClosedAt))
                .ToListAsync(cancellationToken);

            List<OrderDto> activePositions = await context.BacktestOrders
                .Where(o => o.SessionId == request.SessionId && o.Status == BacktestOrderStatus.Active)
                .Select(o => new OrderDto(
                    o.Id, o.OrderType.ToString(), o.Side.ToString(), o.Status.ToString(),
                    o.EntryPrice, o.FilledPrice, o.PositionSize, o.StopLoss, o.TakeProfit,
                    o.ExitPrice, o.Pnl, o.OrderedAt, o.FilledAt, o.ClosedAt))
                .ToListAsync(cancellationToken);

            // Calculate unrealized PnL using latest candle close
            decimal unrealizedPnl = 0m;
            OhlcvCandle? latestCandle = await context.OhlcvCandles
                .Where(c => c.Asset == session.Asset
                            && c.Timeframe == session.ActiveTimeframe
                            && c.Timestamp <= session.CurrentTimestamp)
                .OrderByDescending(c => c.Timestamp)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestCandle is not null)
            {
                foreach (OrderDto pos in activePositions)
                {
                    decimal entryPrice = pos.FilledPrice ?? pos.EntryPrice;
                    unrealizedPnl += pos.Side == "Long"
                        ? (latestCandle.Close - entryPrice) * pos.PositionSize
                        : (entryPrice - latestCandle.Close) * pos.PositionSize;
                }
            }

            // Load drawings
            ChartDrawing? drawing = await context.ChartDrawings
                .FirstOrDefaultAsync(d => d.SessionId == request.SessionId, cancellationToken);

            PlaybackStateDto stateDto = new(
                session.Id,
                session.Asset,
                session.CurrentTimestamp,
                session.ActiveTimeframe.ToString(),
                session.CurrentBalance,
                session.CurrentBalance + unrealizedPnl,
                unrealizedPnl,
                session.Status.ToString(),
                pendingOrders,
                activePositions,
                drawing?.DrawingsJson ?? "[]");

            return Result<PlaybackStateDto>.Success(stateDto);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Playback);

            group.MapGet("/{sessionId:int}/state", async (int sessionId, ISender sender) =>
            {
                Result<PlaybackStateDto> result = await sender.Send(new Request(sessionId));

                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .Produces<Result<PlaybackStateDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Get the full playback state for session resumption.")
            .WithDescription("Returns the current timestamp, balance, orders, positions, and drawings for resuming a session.")
            .WithTags(Tags.BacktestPlayback)
            .RequireAuthorization();
        }
    }
}
