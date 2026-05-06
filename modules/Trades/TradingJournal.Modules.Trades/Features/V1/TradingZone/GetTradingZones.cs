using Mapster;

namespace TradingJournal.Modules.Trades.Features.V1.TradingZone;

public sealed class GetTradingZones
{
    public sealed record Request() : IQuery<Result<IReadOnlyCollection<TradingZoneViewModel>>>;

    public sealed class Handler(ITradeDbContext context, ICacheRepository cacheRepository) : IQueryHandler<Request, Result<IReadOnlyCollection<TradingZoneViewModel>>>
    {
        public async Task<Result<IReadOnlyCollection<TradingZoneViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            var tradingZoneViewModels = await cacheRepository.GetOrCreateAsync<IReadOnlyCollection<TradingZoneViewModel>>(
                CacheKeys.TradingZones,
                async ct =>
                {
                    var tradingZones = await context.TradingZones
                        .AsNoTracking()
                        .ToListAsync(ct);

                    return tradingZones.Adapt<IReadOnlyCollection<TradingZoneViewModel>>();
                },
                expiration: TimeSpan.FromMinutes(10),
                cancellationToken: cancellationToken) ?? [];

            return Result<IReadOnlyCollection<TradingZoneViewModel>>.Success(tradingZoneViewModels);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradingZones);

            group.MapGet("/", async (ISender sender) =>
            {
                Result<IReadOnlyCollection<TradingZoneViewModel>> result = await sender.Send(new Request());

                return result.IsSuccess ? Results.Ok(result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<IReadOnlyCollection<TradingZoneViewModel>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Get all trading zones.")
            .WithDescription("Retrieves all trading zones.")
            .WithTags(Tags.TradingZones)
            .RequireAuthorization();
        }
    }
}