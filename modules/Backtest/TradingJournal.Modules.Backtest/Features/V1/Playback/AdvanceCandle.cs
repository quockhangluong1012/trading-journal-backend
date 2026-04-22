using TradingJournal.Modules.Backtest.Dto;

namespace TradingJournal.Modules.Backtest.Features.V1.Playback;

public sealed class AdvanceCandle
{
    public record Request(int SessionId) : ICommand<Result<AdvanceCandleResponseDto>>
    {
        public int UserId { get; set; }
    }

    internal sealed class Handler(IBacktestDbContext context, IPlaybackEngine playbackEngine)
        : ICommandHandler<Request, Result<AdvanceCandleResponseDto>>
    {
        public async Task<Result<AdvanceCandleResponseDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            // Verify ownership
            bool isOwner = await context.BacktestSessions
                .AnyAsync(s => s.Id == request.SessionId
                               && s.CreatedBy == request.UserId, cancellationToken);

            if (!isOwner)
                return Result<AdvanceCandleResponseDto>.Failure(Error.Create("Session not found."));

            PlaybackAdvanceResult advanceResult = await playbackEngine.AdvanceCandleAsync(request.SessionId, cancellationToken);

            CandleDto? candle = advanceResult.Candle is not null
                ? new CandleDto(
                    advanceResult.Candle.Timestamp,
                    advanceResult.Candle.Open,
                    advanceResult.Candle.High,
                    advanceResult.Candle.Low,
                    advanceResult.Candle.Close,
                    advanceResult.Candle.Volume)
                : null;

            // Map filled and closed orders
            List<OrderDto> filledOrders = advanceResult.MatchingResult?.Fills
                .Select(f =>
                {
                    BacktestOrder order = context.BacktestOrders.Find(f.OrderId)!;
                    return MapOrderToDto(order);
                }).ToList() ?? [];

            List<OrderDto> closedPositions = advanceResult.MatchingResult?.Closes
                .Select(c =>
                {
                    BacktestOrder order = context.BacktestOrders.Find(c.OrderId)!;
                    return MapOrderToDto(order);
                }).ToList() ?? [];

            AdvanceCandleResponseDto response = new(
                candle,
                advanceResult.UpdatedBalance,
                advanceResult.MatchingResult?.Equity ?? advanceResult.UpdatedBalance,
                advanceResult.MatchingResult?.UnrealizedPnl ?? 0m,
                advanceResult.NewTimestamp,
                advanceResult.IsSessionEnded,
                advanceResult.MatchingResult?.IsLiquidated ?? false,
                filledOrders,
                closedPositions);

            return Result<AdvanceCandleResponseDto>.Success(response);
        }

        private static OrderDto MapOrderToDto(BacktestOrder o) => new(
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
            o.ClosedAt);
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Playback);

            group.MapPost("/{sessionId:int}/advance", async (int sessionId, ClaimsPrincipal user, ISender sender) =>
            {
                Result<AdvanceCandleResponseDto> result = await sender.Send(new Request(sessionId) with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<AdvanceCandleResponseDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Advance the playback by one candle.")
            .WithDescription("Fetches the next candle, evaluates all orders, and returns the updated state.")
            .WithTags(Tags.BacktestPlayback)
            .RequireAuthorization();
        }
    }
}
