namespace TradingJournal.Modules.Trades.Features.V1.Dashboard;

public sealed class GetTradingStatistic
{
    public sealed record Request(DashboardFilter Filter, int UserId = 0) : IQuery<Result<TradingStatisticViewModel>>;

    public sealed class Handler(ITradeDbContext context) : IQueryHandler<Request, Result<TradingStatisticViewModel>>
    {
        public async Task<Result<TradingStatisticViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            DateTime fromDate = DashboardFilterHelper.GetFromDate(request.Filter);

            List<TradeHistory> trades = await context.TradeHistories
                .AsNoTracking()
                .Where(t => t.CreatedBy == request.UserId && t.Date >= fromDate)
                .ToListAsync(cancellationToken);

            if (trades.Count == 0)
            {
                return Result<TradingStatisticViewModel>.Success(new TradingStatisticViewModel());
            }

            List<TradeHistory> closedTrades = [.. trades.Where(t => t.Status == TradeStatus.Closed)];

            double totalPnL = closedTrades.Where(t => t.Pnl.HasValue).Sum(t => t.Pnl ?? 0);

            int totalWin = closedTrades.Count(t => t.Pnl is > 0);
            int totalLoss = closedTrades.Count(t => t.Pnl is < 0);

            double winRate = closedTrades.Count == 0 ? 0 : (double)totalWin / (totalWin + totalLoss) * 100;

            int totalTrades = trades.Count;

            int openPositions = trades.Where(x => x.Status == TradeStatus.Open).Count(t => !t.Pnl.HasValue);

            TradingStatisticViewModel statistic = new()
            {
                TotalPnL = totalPnL,
                WinRate = winRate,
                TotalTrades = totalTrades,
                OpenPositions = openPositions
            };

            return Result<TradingStatisticViewModel>.Success(statistic);
        }
    }

    public sealed class Endpoint() : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Dashboard);

            group.MapGet("/statistics", async (DashboardFilter filter, IMediator sender) =>
            {
                Result<TradingStatisticViewModel> result = await sender.Send(new Request(filter));

                return result.IsSuccess ? Results.Ok(result.Value) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result<TradingStatisticViewModel>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Get trading statistics.")
            .WithDescription("Retrieves the trading statistics including total PnL, win rate, total trades, and open positions.")
            .WithTags(Tags.Dashboard)
            .RequireAuthorization();
        }
    }
}
