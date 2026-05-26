using TradingJournal.Modules.Backtest.Dto;

namespace TradingJournal.Modules.Backtest.Features.V1.Analytics;

public sealed class GetSessionAnalytics
{
    public record Request(int SessionId) : IQuery<Result<AnalyticsDto>>
    {
        public int UserId { get; set; }
    }

    internal sealed class Handler(IBacktestDbContext context) : IQueryHandler<Request, Result<AnalyticsDto>>
    {
        public async Task<Result<AnalyticsDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            BacktestSession? session = await context.BacktestSessions
                .FirstOrDefaultAsync(s => s.Id == request.SessionId
                                          && s.CreatedBy == request.UserId, cancellationToken);

            if (session is null)
                return Result<AnalyticsDto>.Failure(Error.Create("Session not found."));

            List<BacktestTradeResult> tradeResults = await context.BacktestTradeResults
                .Where(t => t.SessionId == request.SessionId)
                .OrderBy(t => t.ExitTime)
                .ToListAsync(cancellationToken);

            int totalTrades = tradeResults.Count;
            int totalWins = tradeResults.Count(t => t.Pnl > 0);
            int totalLosses = tradeResults.Count(t => t.Pnl <= 0);
            decimal winRate = totalTrades > 0 ? Math.Round((decimal)totalWins / totalTrades * 100m, 2) : 0m;
            decimal grossProfit = tradeResults.Where(t => t.Pnl > 0).Sum(t => t.Pnl);
            decimal grossLoss = tradeResults.Where(t => t.Pnl <= 0).Sum(t => t.Pnl);
            decimal netPnl = grossProfit + grossLoss;

            // Calculate max drawdown
            decimal maxDrawdown = CalculateMaxDrawdown(session.InitialBalance, tradeResults);

            // Build equity curve
            List<EquityCurvePoint> equityCurve = BuildEquityCurve(session.InitialBalance, tradeResults);

            // Map trade log
            List<TradeResultDto> tradeLog = tradeResults.Select(t => new TradeResultDto(
                t.Id, t.OrderId, t.Side.ToString(), t.EntryPrice, t.ExitPrice,
                t.PositionSize, t.Pnl, t.BalanceAfter, t.EntryTime, t.ExitTime, t.ExitReason))
                .ToList();

            AnalyticsDto analytics = new(
                totalTrades, totalWins, totalLosses, winRate,
                grossProfit, grossLoss, netPnl, maxDrawdown,
                equityCurve, tradeLog);

            return Result<AnalyticsDto>.Success(analytics);
        }

        private static decimal CalculateMaxDrawdown(decimal initialBalance, List<BacktestTradeResult> trades)
        {
            if (trades.Count == 0) return 0m;

            decimal peak = initialBalance;
            decimal maxDrawdown = 0m;

            foreach (BacktestTradeResult trade in trades)
            {
                if (trade.BalanceAfter > peak)
                    peak = trade.BalanceAfter;

                decimal drawdown = peak > 0
                    ? (peak - trade.BalanceAfter) / peak * 100m
                    : 0m;

                if (drawdown > maxDrawdown)
                    maxDrawdown = drawdown;
            }

            return Math.Round(maxDrawdown, 2);
        }

        private static List<EquityCurvePoint> BuildEquityCurve(decimal initialBalance, List<BacktestTradeResult> trades)
        {
            List<EquityCurvePoint> curve =
            [
                new EquityCurvePoint(trades.Count > 0 ? trades[0].EntryTime : DateTime.UtcNow, initialBalance)
            ];

            foreach (BacktestTradeResult trade in trades)
            {
                curve.Add(new EquityCurvePoint(trade.ExitTime, trade.BalanceAfter));
            }

            return curve;
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Analytics);

            group.MapGet("/{sessionId:int}", async (int sessionId, ClaimsPrincipal user, ISender sender) =>
            {
                Result<AnalyticsDto> result = await sender.Send(new Request(sessionId) with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .Produces<Result<AnalyticsDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Get post-session analytics and trade log.")
            .WithDescription("Returns win rate, PnL, max drawdown, equity curve, and full trade log.")
            .WithTags(Tags.BacktestAnalytics)
            .RequireAuthorization();
        }
    }
}
