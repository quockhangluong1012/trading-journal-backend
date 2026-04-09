using TradingJournal.Shared.Common;


namespace TradingJournal.Modules.Trades.Features.V1.Dashboard;

public sealed class GetProfitTrajectory
{
    public sealed record Request(DashboardFilter Filter, int UserId = 0) : IQuery<Result<IReadOnlyCollection<ProfitTrajectoryViewModel>>>;

    public sealed record ProfitTrajectoryViewModel(DateTime Date, double? PnL = 0);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Filter)
            .Cascade(CascadeMode.Stop)
            .IsInEnum()
            .WithErrorCode(HttpStatusCode.BadRequest.ToString())
            .WithMessage("Invalid filter value. Allowed values are: OneDay, OneWeek, OneMonth, ThreeMonths, AllTime.");
        }
    }

    public sealed class Handler(ITradeDbContext context) : IQueryHandler<Request, Result<IReadOnlyCollection<ProfitTrajectoryViewModel>>>
    {
        public async Task<Result<IReadOnlyCollection<ProfitTrajectoryViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            DateTime fromDate = DashboardFilterHelper.GetFromDate(request.Filter);

            List<TradeHistory>? trades = await context.TradeHistories
                .AsNoTracking()
                .Where(t => t.CreatedBy == request.UserId && t.Status == TradeStatus.Closed && t.ClosedDate != null && t.ClosedDate.Value >= fromDate)
                .ToListAsync(cancellationToken);

            if (trades is null || trades.Count == 0)
            {
                return Result<IReadOnlyCollection<ProfitTrajectoryViewModel>>.Success([]);
            }

            DateTime currentDate = new DateTimeProvider().Now;

            List<ProfitTrajectoryViewModel> trajectory = [];

            for (DateTime date = fromDate.Date; date <= currentDate.Date; date = date.AddDays(1))
            {
                trades.Where(x => x.ClosedDate != null && x.ClosedDate.Value.Date == date.Date && x.Pnl.HasValue)
                .ToList()
                .ForEach(t =>
                {
                    trajectory.Add(new ProfitTrajectoryViewModel(date, t.Pnl.Value));
                });
            }

            return Result<IReadOnlyCollection<ProfitTrajectoryViewModel>>.Success(trajectory);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Dashboard);

            group.MapGet("/profit-trajectory", async (DashboardFilter filter, IMediator sender) =>
            {
                Result<IReadOnlyCollection<ProfitTrajectoryViewModel>> result = await sender.Send(new Request(filter));

                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
             .Produces<Result<IReadOnlyCollection<ProfitTrajectoryViewModel>>>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status400BadRequest)
             .Produces(StatusCodes.Status500InternalServerError)
             .WithSummary("Get profit trajectory.")
             .WithDescription("Retrieves the profit trajectory based on the specified filter.")
             .WithTags(Tags.Dashboard)
             .RequireAuthorization();
        }
    }
}