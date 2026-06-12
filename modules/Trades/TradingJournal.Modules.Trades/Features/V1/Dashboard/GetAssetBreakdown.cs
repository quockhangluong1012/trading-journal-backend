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

            List<AssetBreakdownDto> breakdowns = await tradeProvider.GetAssetBreakdownsAsync(request.UserId, fromDate, cancellationToken);

            List<AssetBreakdownViewModel> assetGroups = [.. breakdowns
                .Select(b => new AssetBreakdownViewModel(
                    b.Asset,
                    Math.Round(b.TotalPnl, 2),
                    b.TradeCount,
                    Math.Round((decimal)b.WinCount / b.TradeCount * 100, 1)))];

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