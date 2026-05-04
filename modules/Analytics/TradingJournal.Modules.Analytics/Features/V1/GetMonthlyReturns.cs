using TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Modules.Analytics.Features.V1;

public sealed class GetMonthlyReturns
{
    internal sealed record Request(AnalyticsFilter Filter, int UserId = 0) : IQuery<Result<IReadOnlyCollection<MonthlyReturnViewModel>>>;

    internal sealed record MonthlyReturnViewModel(string Month, decimal Pnl);

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

    internal sealed class Handler(ITradeProvider tradeProvider) : IQueryHandler<Request, Result<IReadOnlyCollection<MonthlyReturnViewModel>>>
    {
        public async Task<Result<IReadOnlyCollection<MonthlyReturnViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            List<TradeCacheDto> trades = await tradeProvider.GetTradesAsync(request.UserId, cancellationToken);
            DateTimeOffset fromDate = AnalyticsFilterHelper.GetFromDate(request.Filter);

            List<TradeCacheDto> closed = [.. trades
                .Where(t => t.Status == TradeStatus.Closed && t.Pnl.HasValue && t.ClosedDate.HasValue)
                .Where(t => fromDate == DateTimeOffset.MinValue || t.ClosedDate!.Value >= fromDate)];

            Dictionary<string, decimal> monthly = [];
            foreach (TradeCacheDto t in closed)
            {
                string key = $"{t.ClosedDate!.Value.Year}-{t.ClosedDate.Value.Month:D2}";
                monthly.TryAdd(key, 0);
                monthly[key] += (decimal)t.Pnl!.Value;
            }

            List<MonthlyReturnViewModel> result = monthly
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => new MonthlyReturnViewModel(kvp.Key, Math.Round(kvp.Value, 2)))
                .ToList();

            return Result<IReadOnlyCollection<MonthlyReturnViewModel>>.Success(result);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/analytics");

            group.MapGet("/monthly-returns", async (AnalyticsFilter filter, ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request(filter) with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result<IReadOnlyCollection<MonthlyReturnViewModel>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get monthly returns.")
            .WithDescription("Retrieves PnL aggregated by month.")
            .WithTags(Tags.Analytics)
            .RequireAuthorization();
        }
    }
}
