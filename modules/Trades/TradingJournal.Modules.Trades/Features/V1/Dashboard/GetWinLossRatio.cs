

namespace TradingJournal.Modules.Trades.Features.V1.Dashboard;

public sealed class GetWinLossRatio
{
    public sealed record Request(DashboardFilter Filter, int UserId = 0) : IQuery<Result<IReadOnlyCollection<WinLossRatioViewModel>>>;

    public sealed class Handler(ITradeDbContext context) : IQueryHandler<Request, Result<IReadOnlyCollection<WinLossRatioViewModel>>>
    {
        public async Task<Result<IReadOnlyCollection<WinLossRatioViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            DateTime fromDate = DashboardFilterHelper.GetFromDate(request.Filter);

            List<TradeHistory>? trades = await context.TradeHistories
                .AsNoTracking()
                .Where(t => t.CreatedBy == request.UserId && t.Status == TradeStatus.Closed && t.Pnl.HasValue && t.ClosedDate != null && t.ClosedDate.Value >= fromDate)
                .ToListAsync(cancellationToken);

            if (trades is null || trades.Count == 0)
            {
                return Result<IReadOnlyCollection<WinLossRatioViewModel>>.Success([]);
            }

            int wins = trades.Count(t => t.Pnl.HasValue && t.Pnl.Value > 0);
            int losses = trades.Count(t => t.Pnl.HasValue && t.Pnl.Value < 0);

            IReadOnlyCollection<WinLossRatioViewModel> winLossRatios = [
                new WinLossRatioViewModel("Wins", wins),
                new WinLossRatioViewModel("Losses", losses)
            ];

            return Result<IReadOnlyCollection<WinLossRatioViewModel>>.Success(winLossRatios);
        }
    }

    public sealed record WinLossRatioViewModel(string Name, double Value);

    public sealed class Endpoint() : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Dashboard);

            group.MapGet("/win-loss-ratio", async (DashboardFilter filter, IMediator sender) =>
            {
                Result<IReadOnlyCollection<WinLossRatioViewModel>> result = await sender.Send(new Request(filter));

                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result<IReadOnlyCollection<WinLossRatioViewModel>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Get win/loss ratio.")
            .WithDescription("Retrieves the count of winning and losing trades for the user.")
            .WithTags(Tags.Dashboard)
            .RequireAuthorization();
        }
    }
}