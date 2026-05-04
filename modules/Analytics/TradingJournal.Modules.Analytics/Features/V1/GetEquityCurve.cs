using TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Modules.Analytics.Features.V1;

public sealed class GetEquityCurve
{
    internal sealed record Request(AnalyticsFilter Filter, int UserId = 0) : IQuery<Result<IReadOnlyCollection<EquityPointViewModel>>>;

    internal sealed record EquityPointViewModel(DateTimeOffset Date, decimal Profit);

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

    internal sealed class Handler(ITradeProvider tradeProvider) : IQueryHandler<Request, Result<IReadOnlyCollection<EquityPointViewModel>>>
    {
        public async Task<Result<IReadOnlyCollection<EquityPointViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            List<TradeCacheDto> trades = await tradeProvider.GetTradesAsync(request.UserId, cancellationToken);
            DateTimeOffset fromDate = AnalyticsFilterHelper.GetFromDate(request.Filter);

            List<TradeCacheDto> closed = [.. trades
                .Where(t => t.Status == TradeStatus.Closed && t.Pnl.HasValue && t.ClosedDate.HasValue)
                .Where(t => fromDate == DateTimeOffset.MinValue || t.ClosedDate!.Value >= fromDate)
                .OrderBy(t => t.ClosedDate!.Value)];

            decimal cumulativeProfit = 0;
            List<EquityPointViewModel> result = [.. closed
                .Select(t =>
                {
                    cumulativeProfit += (decimal)t.Pnl!.Value;
                    return new EquityPointViewModel(t.ClosedDate!.Value, Math.Round(cumulativeProfit, 2));
                })];

            return Result<IReadOnlyCollection<EquityPointViewModel>>.Success(result);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/analytics");

            group.MapGet("/equity-curve", async (AnalyticsFilter filter, ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request(filter) with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result<IReadOnlyCollection<EquityPointViewModel>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get equity curve.")
            .WithDescription("Retrieves cumulative profit trajectory over time.")
            .WithTags(Tags.Analytics)
            .RequireAuthorization();
        }
    }
}
