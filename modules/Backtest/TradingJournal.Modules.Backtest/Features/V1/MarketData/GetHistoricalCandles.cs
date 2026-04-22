using TradingJournal.Modules.Backtest.Dto;

namespace TradingJournal.Modules.Backtest.Features.V1.MarketData;

public sealed class GetHistoricalCandles
{
    public record Request(
        int SessionId,
        string? Timeframe,
        int Page = 1,
        int PageSize = 500) : IQuery<Result<List<CandleDto>>>
    {
        public int UserId { get; set; }
    }

    internal sealed class Handler(
        IBacktestDbContext context,
        ICandleAggregationService aggregationService) : IQueryHandler<Request, Result<List<CandleDto>>>
    {
        public async Task<Result<List<CandleDto>>> Handle(Request request, CancellationToken cancellationToken)
        {
            BacktestSession? session = await context.BacktestSessions
                .FirstOrDefaultAsync(s => s.Id == request.SessionId
                                          && s.CreatedBy == request.UserId, cancellationToken);

            if (session is null)
                return Result<List<CandleDto>>.Failure(Error.Create("Session not found."));

            Timeframe tf = request.Timeframe is not null
                ? Enum.Parse<Timeframe>(request.Timeframe, ignoreCase: true)
                : session.ActiveTimeframe;

            // Use the asset symbol as stored (already normalized when asset was created)
            string symbol = session.Asset;

            DateTime fromDate = session.StartDate;

            // CRITICAL: Only return candles up to the current simulated timestamp
            // to prevent look-ahead bias. We load from session.StartDate to start the chart empty.
            List<OhlcvCandle> aggregated = await aggregationService.AggregateAsync(
                symbol, tf, fromDate, session.CurrentTimestamp, cancellationToken);

            List<CandleDto> candles = aggregated
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(c => new CandleDto(
                    c.Timestamp, c.Open, c.High, c.Low, c.Close, c.Volume))
                .ToList();

            return Result<List<CandleDto>>.Success(candles);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.MarketData);

            group.MapGet("/{sessionId:int}/candles", async (int sessionId,
                [FromQuery] string? timeframe, [FromQuery] int page, [FromQuery] int pageSize, ClaimsPrincipal user, ISender sender) =>
            {
                if (page <= 0) page = 1;
                if (pageSize <= 0 || pageSize > 2000) pageSize = 500;

                Result<List<CandleDto>> result = await sender.Send(
                    new Request(sessionId, timeframe, page, pageSize) { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<List<CandleDto>>>(StatusCodes.Status200OK)
            .WithSummary("Get historical OHLCV candles for a session.")
            .WithDescription("Returns M1 candles aggregated to the requested timeframe, up to the current simulated timestamp (no look-ahead bias).")
            .WithTags(Tags.BacktestMarketData)
            .RequireAuthorization();
        }
    }
}
