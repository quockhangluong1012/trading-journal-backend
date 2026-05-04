using TradingJournal.Shared.Common;


namespace TradingJournal.Modules.Trades.Features.V1.Dashboard;

public sealed class GetProfitTrajectory
{
    public sealed record Request(DashboardFilter Filter, int UserId = 0) : IQuery<Result<IReadOnlyCollection<ProfitTrajectoryViewModel>>>;

    public sealed record ProfitTrajectoryViewModel(DateTimeOffset Date, decimal? PnL = 0);

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
            DateTimeOffset fromDate = DashboardFilterHelper.GetFromDate(request.Filter);

            List<ProfitTrajectoryViewModel> trajectory = await context.TradeHistories
                .AsNoTracking()
                .Where(t => t.CreatedBy == request.UserId && t.Status == TradeStatus.Closed && t.ClosedDate != null && t.ClosedDate >= fromDate && t.Pnl.HasValue)
                .OrderBy(t => t.ClosedDate)
                .Select(t => new ProfitTrajectoryViewModel(t.ClosedDate!.Value, t.Pnl!.Value))
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyCollection<ProfitTrajectoryViewModel>>.Success(trajectory);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Dashboard);

            group.MapGet("/profit-trajectory", async (DashboardFilter filter, ClaimsPrincipal user, IMediator sender) =>
            {
                Result<IReadOnlyCollection<ProfitTrajectoryViewModel>> result = await sender.Send(new Request(filter) with { UserId = user.GetCurrentUserId() });

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