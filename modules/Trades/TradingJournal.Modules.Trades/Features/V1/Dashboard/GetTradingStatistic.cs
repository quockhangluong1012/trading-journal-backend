using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Trades.Features.V1.Dashboard;

public sealed class GetTradingStatistic
{
    public sealed record Request(DashboardFilter Filter, int UserId = 0) : IQuery<Result<TradingStatisticViewModel>>;

    public sealed class Handler(ITradeProvider tradeProvider) : IQueryHandler<Request, Result<TradingStatisticViewModel>>
    {
        public async Task<Result<TradingStatisticViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            DateTime fromDate = DashboardFilterHelper.GetFromDate(request.Filter);

            List<TradeCacheDto> allTrades = await tradeProvider.GetTradesAsync(request.UserId, cancellationToken);

            List<TradeCacheDto> trades = [.. allTrades.Where(t => t.Date >= fromDate)];

            if (trades.Count == 0)
            {
                return Result<TradingStatisticViewModel>.Success(new TradingStatisticViewModel());
            }

            List<TradeCacheDto> closedTrades = [.. trades.Where(t => t.Status == TradeStatus.Closed)];

            decimal totalPnL = closedTrades.Where(t => t.Pnl.HasValue).Sum(t => t.Pnl ?? 0);

            int totalWin = closedTrades.Count(t => t.Pnl is > 0);
            int totalLoss = closedTrades.Count(t => t.Pnl is < 0);

            decimal winRate = closedTrades.Count == 0 ? 0 : (decimal)totalWin / (totalWin + totalLoss) * 100;

            int totalTrades = trades.Count;

            int openPositions = trades.Count(t => t.Status == TradeStatus.Open && !t.Pnl.HasValue);

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

            group.MapGet("/statistics", async (DashboardFilter filter, ClaimsPrincipal user, IMediator sender) =>
            {
                Result<TradingStatisticViewModel> result = await sender.Send(new Request(filter) with { UserId = user.GetCurrentUserId() });

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
