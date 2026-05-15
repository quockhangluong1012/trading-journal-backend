using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Trades.Features.V1.Trade;

public sealed class GetTradeAssets
{
    private const int MaxAssetOptions = 50;

    public sealed record Request(int UserId) : IQuery<Result<IReadOnlyCollection<string>>>;

    public sealed class Handler(ITradeDbContext context) : IQueryHandler<Request, Result<IReadOnlyCollection<string>>>
    {
        public async Task<Result<IReadOnlyCollection<string>>> Handle(Request request, CancellationToken cancellationToken)
        {
            if (request.UserId <= 0)
            {
                return Result<IReadOnlyCollection<string>>.Failure(Error.Create("A valid user context is required."));
            }

            IReadOnlyCollection<string> assets = await context.TradeHistories
                .AsNoTracking()
                .Where(trade => trade.CreatedBy == request.UserId)
                .Where(trade => !string.IsNullOrWhiteSpace(trade.Asset))
                .Select(trade => new
                {
                    Asset = trade.Asset.Trim().ToUpper(),
                    trade.Date,
                })
                .GroupBy(trade => trade.Asset)
                .Select(group => new
                {
                    Asset = group.Key,
                    LastTradedAt = group.Max(trade => trade.Date),
                })
                .OrderByDescending(group => group.LastTradedAt)
                .ThenBy(group => group.Asset)
                .Take(MaxAssetOptions)
                .Select(group => group.Asset)
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyCollection<string>>.Success(assets);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradeHistory);

            group.MapGet("/assets", async (ClaimsPrincipal user, ISender sender) =>
            {
                Result<IReadOnlyCollection<string>> result = await sender.Send(new Request(user.GetCurrentUserId()));

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<IReadOnlyCollection<string>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Get trade assets.")
            .WithDescription("Retrieves distinct persisted asset names for the current user.")
            .WithTags(Tags.TradeHistory)
            .RequireAuthorization();
        }
    }
}