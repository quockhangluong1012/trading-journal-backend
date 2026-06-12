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

            TradeStatisticsDto stats = await tradeProvider.GetTradeStatisticsAsync(request.UserId, fromDate, cancellationToken);

            if (stats.ClosedTrades == 0)
            {
                return Result<TradingStatisticViewModel>.Success(new TradingStatisticViewModel());
            }

            decimal winRate = stats.ClosedTrades == 0 ? 0 : (decimal)stats.WinCount / stats.ClosedTrades * 100;

            TradingStatisticViewModel statistic = new()
            {
                TotalPnL = stats.TotalPnl,
                WinRate = winRate,
                TotalTrades = stats.TotalTrades,
                OpenPositions = stats.OpenPositions
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
