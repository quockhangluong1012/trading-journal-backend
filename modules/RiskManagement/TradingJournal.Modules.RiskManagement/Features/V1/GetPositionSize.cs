using TradingJournal.Modules.RiskManagement.Common.Helpers;

namespace TradingJournal.Modules.RiskManagement.Features.V1;

public sealed class GetPositionSize
{
    internal sealed record Request(
        decimal EntryPrice, decimal StopLossPrice,
        decimal? AccountBalanceOverride, decimal? RiskPercentOverride,
        int UserId = 0) : IQuery<Result<PositionSizeViewModel>>;

    internal sealed record PositionSizeViewModel(
        decimal AccountBalance, decimal RiskPercent, decimal RiskAmount,
        decimal Units, decimal Lots, decimal StopLossDistance, decimal StopLossDistancePips);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.EntryPrice).GreaterThan(0);
            RuleFor(x => x.StopLossPrice).GreaterThan(0);
        }
    }

    internal sealed class Handler(IRiskDbContext context) : IQueryHandler<Request, Result<PositionSizeViewModel>>
    {
        public async Task<Result<PositionSizeViewModel>> Handle(Request request, CancellationToken ct)
        {
            RiskConfig? config = await context.RiskConfigs.AsNoTracking()
                .FirstOrDefaultAsync(x => x.CreatedBy == request.UserId, ct);

            decimal balance = request.AccountBalanceOverride ?? config?.AccountBalance ?? 10000m;
            decimal riskPct = request.RiskPercentOverride ?? config?.RiskPerTradePercent ?? 1.0m;

            var sizing = PositionSizingCalculator.Calculate(balance, riskPct, request.EntryPrice, request.StopLossPrice);
            decimal slDist = Math.Abs(request.EntryPrice - request.StopLossPrice);

            return Result<PositionSizeViewModel>.Success(new(
                Math.Round(balance, 2), riskPct, Math.Round(sizing.RiskAmount, 2),
                sizing.Units, sizing.Lots, Math.Round(slDist, 5), Math.Round(slDist * 10000m, 1)));
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGroup("api/v1/risk").MapGet("/position-size", async (
                decimal entryPrice, decimal stopLossPrice,
                decimal? accountBalance, decimal? riskPercent,
                ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request(entryPrice, stopLossPrice, accountBalance, riskPercent)
                    with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result<PositionSizeViewModel>>(StatusCodes.Status200OK)
            .WithSummary("Calculate position size.")
            .WithTags(Tags.RiskManagement).RequireAuthorization();
        }
    }
}
