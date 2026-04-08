using TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Modules.Analytics.Features.V1;

public sealed class GetDayOfWeekBreakdown
{
    internal sealed record Request(AnalyticsFilter Filter, int UserId = 0) : IQuery<Result<IReadOnlyCollection<DayOfWeekViewModel>>>;

    internal sealed record DayOfWeekViewModel(string Day, double Pnl, int Count, double WinRate);

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

    internal sealed class Handler(ITradeProvider tradeProvider) : IQueryHandler<Request, Result<IReadOnlyCollection<DayOfWeekViewModel>>>
    {
        public async Task<Result<IReadOnlyCollection<DayOfWeekViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            List<TradeCacheDto> allTrades = await tradeProvider.GetTradesAsync(cancellationToken);
            List<TradeCacheDto> trades = [.. allTrades.Where(t => t.CreatedBy == request.UserId)];
            DateTime fromDate = AnalyticsFilterHelper.GetFromDate(request.Filter);

            List<TradeCacheDto> closed = [.. trades
                .Where(t => t.Status == TradeStatus.Closed && t.Pnl.HasValue && t.ClosedDate.HasValue)
                .Where(t => fromDate == DateTime.MinValue || t.ClosedDate!.Value >= fromDate)];

            string[] dayNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

            var dayGroups = closed
                .GroupBy(t => t.ClosedDate!.Value.DayOfWeek)
                .ToDictionary(g => g.Key, g => g.ToList());

            List<DayOfWeekViewModel> result = [.. Enum.GetValues<DayOfWeek>()
                .Select(day =>
                {
                    if (!dayGroups.TryGetValue(day, out List<TradeCacheDto>? trades) || trades.Count == 0)
                    {
                        return new DayOfWeekViewModel(dayNames[(int)day], 0, 0, 0);
                    }

                    return new DayOfWeekViewModel(
                        dayNames[(int)day],
                        Math.Round(trades.Sum(t => (double)t.Pnl!.Value), 2),
                        trades.Count,
                        Math.Round((double)trades.Count(t => t.Pnl > 0) / trades.Count * 100, 1));
                })];

            return Result<IReadOnlyCollection<DayOfWeekViewModel>>.Success(result);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/analytics");

            group.MapGet("/day-of-week", async (AnalyticsFilter filter, ISender sender) =>
            {
                var result = await sender.Send(new Request(filter));
                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result<IReadOnlyCollection<DayOfWeekViewModel>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get day of week breakdown.")
            .WithDescription("Retrieves performance breakdown by day of the week.")
            .WithTags(Tags.Analytics)
            .RequireAuthorization();
        }
    }
}
