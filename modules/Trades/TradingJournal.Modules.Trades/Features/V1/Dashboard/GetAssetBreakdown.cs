using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Trades.Features.V1.Dashboard;

public sealed class GetAssetBreakdown
{
    public sealed record Request(DashboardFilter Filter, int UserId = 0) : IQuery<Result<IReadOnlyCollection<AssetBreakdownViewModel>>>;

    public sealed record AssetBreakdownViewModel(string Asset, decimal Pnl, int Count, decimal WinRate);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Filter)
                .IsInEnum()
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Invalid filter value.");
        }
    }

    public sealed class Handler(ITradeProvider tradeProvider) : IQueryHandler<Request, Result<IReadOnlyCollection<AssetBreakdownViewModel>>>
    {
        public async Task<Result<IReadOnlyCollection<AssetBreakdownViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            DateTime fromDate = DashboardFilterHelper.GetFromDate(request.Filter);

            List<TradeCacheDto> allTrades = await tradeProvider.GetTradesAsync(request.UserId, cancellationToken);

            List<AssetBreakdownViewModel> assetGroups = [.. allTrades
                .Where(trade => trade.Status == TradeStatus.Closed && trade.Pnl.HasValue)
                .Where(trade => fromDate == DateTime.MinValue || (trade.ClosedDate.HasValue && trade.ClosedDate.Value >= fromDate))
                .Where(trade => !string.IsNullOrWhiteSpace(trade.Asset))
                .GroupBy(trade => trade.Asset)
                .Select(group => new AssetBreakdownViewModel(
                    group.Key,
                    Math.Round(group.Sum(trade => (decimal)trade.Pnl!.Value), 2),
                    group.Count(),
                    Math.Round((decimal)group.Count(trade => trade.Pnl > 0) / group.Count() * 100, 1)))
                .OrderByDescending(asset => Math.Abs(asset.Pnl))
                .ThenByDescending(asset => asset.Count)
                .ThenBy(asset => asset.Asset)];

            return Result<IReadOnlyCollection<AssetBreakdownViewModel>>.Success(assetGroups);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Dashboard);

            group.MapGet("/asset-breakdown", async (DashboardFilter filter, ClaimsPrincipal user, IMediator sender) =>
            {
                Result<IReadOnlyCollection<AssetBreakdownViewModel>> result = await sender.Send(new Request(filter) with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result<IReadOnlyCollection<AssetBreakdownViewModel>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Get asset breakdown.")
            .WithDescription("Retrieves dashboard performance grouped by trading asset.")
            .WithTags(Tags.Dashboard)
            .RequireAuthorization();
        }
    }
}