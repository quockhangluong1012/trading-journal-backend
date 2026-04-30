using TradingJournal.Modules.RiskManagement.Common.Helpers;
using TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Modules.RiskManagement.Features.V1;

public sealed class GetCorrelationMatrix
{
    internal sealed record Request(int UserId = 0) : IQuery<Result<CorrelationMatrixViewModel>>;

    internal sealed record CorrelationMatrixViewModel(
        List<string> Assets,
        List<CorrelationPair> Pairs,
        List<CorrelationWarning> Warnings);

    internal sealed record CorrelationPair(string Asset1, string Asset2, decimal Correlation);
    internal sealed record CorrelationWarning(string Severity, string Message, string Asset1, string Asset2, decimal Correlation);

    internal sealed class Handler(ITradeProvider tradeProvider) : IQueryHandler<Request, Result<CorrelationMatrixViewModel>>
    {
        public async Task<Result<CorrelationMatrixViewModel>> Handle(Request request, CancellationToken ct)
        {
            var trades = await tradeProvider.GetTradesAsync(request.UserId, ct);
            var openAssets = trades.Where(t => t.Status == TradeStatus.Open).Select(t => t.Asset).Distinct().ToList();

            if (openAssets.Count == 0)
                return Result<CorrelationMatrixViewModel>.Success(new([], [], []));

            var pairs = new List<CorrelationPair>();
            var warnings = new List<CorrelationWarning>();

            for (int i = 0; i < openAssets.Count; i++)
            {
                for (int j = i + 1; j < openAssets.Count; j++)
                {
                    decimal corr = CorrelationData.GetCorrelation(openAssets[i], openAssets[j]);
                    pairs.Add(new(openAssets[i], openAssets[j], corr));

                    if (corr >= 0.7m)
                        warnings.Add(new("warning", $"{openAssets[i]} and {openAssets[j]} are highly correlated ({corr:F2}). You may have concentrated risk.",
                            openAssets[i], openAssets[j], corr));
                    else if (corr <= -0.7m)
                        warnings.Add(new("info", $"{openAssets[i]} and {openAssets[j]} are inversely correlated ({corr:F2}). Positions may offset each other.",
                            openAssets[i], openAssets[j], corr));
                }
            }

            return Result<CorrelationMatrixViewModel>.Success(new(openAssets, pairs, warnings));
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGroup("api/v1/risk").MapGet("/correlation", async (ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request() with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result<CorrelationMatrixViewModel>>(StatusCodes.Status200OK)
            .WithSummary("Get correlation matrix for open positions.")
            .WithTags(Tags.RiskManagement).RequireAuthorization();
        }
    }
}
