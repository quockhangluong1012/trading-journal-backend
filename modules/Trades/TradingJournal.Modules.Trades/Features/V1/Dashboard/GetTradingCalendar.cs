namespace TradingJournal.Modules.Trades.Features.V1.Dashboard;

public sealed class GetTradingCalendar
{
    public sealed record Request(int Month, int Year, DateTime? Date, DashboardFilter Filter, int UserId = 0) : IQuery<Result<TradingCalendarResponse>>;

    public sealed record TradingCalendarViewModel(DateTime Date, double? PnL = 0);

    public sealed record TradingCalendarResponse(
        double MonthlyPnL,
        double WeeklyPnL,
        double DailyPnL,
        IReadOnlyCollection<TradingCalendarViewModel> Data
    );

    public sealed class Handler(ITradeDbContext context) : IQueryHandler<Request, Result<TradingCalendarResponse>>
    {
        public async Task<Result<TradingCalendarResponse>> Handle(Request request, CancellationToken cancellationToken)
        {
            DateTime filterFromDate = DashboardFilterHelper.GetFromDate(request.Filter);

            DateTime targetDate = request.Date ?? new DateTime(request.Year, request.Month, 1);
            DateTime startOfMonth = new DateTime(targetDate.Year, targetDate.Month, 1);
            DateTime endOfMonth = startOfMonth.AddMonths(1).AddTicks(-1);

            DateTime startOfWeek = targetDate.Date.AddDays(-(int)targetDate.DayOfWeek); // Sunday as start of week
            DateTime endOfWeek = startOfWeek.AddDays(7).AddTicks(-1);

            DateTime startOfDay = targetDate.Date;
            DateTime endOfDay = targetDate.Date.AddDays(1).AddTicks(-1);

            List<TradeHistory>? trades = await context.TradeHistories
                .AsNoTracking()
                .Where(t => t.CreatedBy == request.UserId && t.Status == TradeStatus.Closed && t.ClosedDate != null &&
                    t.ClosedDate.Value.Month == request.Month && t.ClosedDate.Value.Year == request.Year &&
                    t.ClosedDate.Value >= filterFromDate)
                .ToListAsync(cancellationToken);

            List<TradingCalendarViewModel> calendars = [];

            DateTime firstDayOfMonth = new(request.Year, request.Month, 1);
            DateTime lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            for (DateTime date = firstDayOfMonth; date <= lastDayOfMonth; date = date.AddDays(1))
            {
                trades.Where(x => x.ClosedDate != null && x.ClosedDate.Value.Date == date.Date)
                .ToList()
                .ForEach(t =>
                {
                    calendars.Add(new TradingCalendarViewModel(date, t.Pnl ?? 0));
                });
            }

            double monthlyPnL = await context.TradeHistories
                .Where(t => t.CreatedBy == request.UserId && t.Status == TradeStatus.Closed && t.ClosedDate != null &&
                            t.ClosedDate >= startOfMonth && t.ClosedDate <= endOfMonth && t.ClosedDate >= filterFromDate)
                .SumAsync(t => t.Pnl ?? 0, cancellationToken);

            double weeklyPnL = await context.TradeHistories
                .Where(t => t.CreatedBy == request.UserId && t.Status == TradeStatus.Closed && t.ClosedDate != null &&
                            t.ClosedDate >= startOfWeek && t.ClosedDate <= endOfWeek && t.ClosedDate >= filterFromDate)

                .SumAsync(t => t.Pnl ?? 0, cancellationToken);
                
            double dailyPnL = await context.TradeHistories
                .Where(t => t.CreatedBy == request.UserId && t.Status == TradeStatus.Closed && t.ClosedDate != null && 
                            t.ClosedDate >= startOfDay && t.ClosedDate <= endOfDay && t.ClosedDate >= filterFromDate)
                .SumAsync(t => t.Pnl ?? 0, cancellationToken);

            return Result<TradingCalendarResponse>.Success(new TradingCalendarResponse(
                monthlyPnL,
                weeklyPnL,
                dailyPnL,
                calendars
            ));
        }
    }

    public sealed class Endpoint() : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Dashboard);

            group.MapGet("/calendar", async (int month, int year, DateTime? date, DashboardFilter filter, IMediator sender) =>
            {
                Result<TradingCalendarResponse> result = await sender.Send(new Request(month, year, date, filter));

                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
             .Produces<Result<TradingCalendarResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Get trading calendar for a specific month and year.")
            .WithDescription("Retrieves the trading calendar with PnL for each day in the specified month and year, along with daily, weekly, and monthly summaries.")
            .WithTags(Tags.Dashboard)
            .RequireAuthorization();
        }
    }
}