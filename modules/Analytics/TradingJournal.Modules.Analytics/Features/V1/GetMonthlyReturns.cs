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
            DateTime fromDate = AnalyticsFilterHelper.GetFromDate(request.Filter);

            List<MonthlyPnlDto> monthly = await tradeProvider.GetMonthlyPnlAsync(request.UserId, fromDate, cancellationToken);

            List<MonthlyReturnViewModel> result = monthly
                .Select(m => new MonthlyReturnViewModel(m.Month, m.Pnl))
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
