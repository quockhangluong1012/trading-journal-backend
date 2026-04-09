using Mapster;

namespace TradingJournal.Modules.Trades.Features.V1.TradingZone;

public sealed class GetTradingZoneDetail
{
    public sealed record Request(int Id) : IQuery<Result<TradingZoneViewModel>>;

    public sealed class Handler(ITradeDbContext context) : IQueryHandler<Request, Result<TradingZoneViewModel>>
    {
        public async Task<Result<TradingZoneViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            Domain.TradingZone? tradingZone = await context.TradingZones.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

            if (tradingZone is null)
            {
                return Result<TradingZoneViewModel>.Failure(Error.NotFound);
            }

            return Result<TradingZoneViewModel>.Success(tradingZone.Adapt<TradingZoneViewModel>());
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradingZones);

            group.MapGet("/{id:int}", async (int id, ISender sender) =>
            {
                Result<TradingZoneViewModel> result = await sender.Send(new Request(id));

                return result.IsSuccess ? Results.Ok(result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<TradingZoneViewModel>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Get a trading zone by ID.")
            .WithDescription("Retrieves a trading zone by its ID.")
            .WithTags(Tags.TradingZones)
            .RequireAuthorization();
        }
    }
}