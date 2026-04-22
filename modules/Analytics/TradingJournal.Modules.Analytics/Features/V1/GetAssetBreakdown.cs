using TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Modules.Analytics.Features.V1;

public sealed class GetAssetBreakdown
{
    internal sealed record Request(AnalyticsFilter Filter, int UserId = 0) : IQuery<Result<IReadOnlyCollection<AssetBreakdownViewModel>>>;

    internal sealed record AssetBreakdownViewModel(string Asset, decimal Pnl, int Count, decimal WinRate);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Filter)
                .IsInEnum()
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Invalid filter value.");
        }
    }

    internal sealed class Handler(ITradeProvider tradeProvider) : IQueryHandler<Request, Result<IReadOnlyCollection<AssetBreakdownViewModel>>>
    {
        public async Task<Result<IReadOnlyCollection<AssetBreakdownViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            List<TradeCacheDto> trades = await tradeProvider.GetTradesAsync(request.UserId, cancellationToken);
            DateTime fromDate = AnalyticsFilterHelper.GetFromDate(request.Filter);

            List<TradeCacheDto> closed = [.. trades
                .Where(t => t.Status == TradeStatus.Closed && t.Pnl.HasValue)
                .Where(t => fromDate == DateTime.MinValue || (t.ClosedDate.HasValue && t.ClosedDate.Value >= fromDate))];

            var assetGroups = closed
                .GroupBy(t => t.Asset)
                .Select(g => new AssetBreakdownViewModel(
                    g.Key,
                    Math.Round(g.Sum(t => (decimal)t.Pnl!.Value), 2),
                    g.Count(),
                    Math.Round((decimal)g.Count(t => t.Pnl > 0) / g.Count() * 100, 1)))
                .OrderByDescending(a => a.Pnl)
                .ToList();

            return Result<IReadOnlyCollection<AssetBreakdownViewModel>>.Success(assetGroups);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/analytics");

            group.MapGet("/asset-breakdown", async (AnalyticsFilter filter, ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request(filter) with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result<IReadOnlyCollection<AssetBreakdownViewModel>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get asset breakdown.")
            .WithDescription("Retrieves performance breakdown by trading asset.")
            .WithTags(Tags.Analytics)
            .RequireAuthorization();
        }
    }
}
