namespace TradingJournal.Modules.Trades.Features.V1.TradingZone;

public sealed class DeleteTradingZone
{
    public sealed record Request(int Id, int UserId = 0) : ICommand<Result<bool>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Id)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0).WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Trading Zone Id must be greater than 0.");
        }
    }

    public sealed class Handler(ITradeDbContext context, ICacheRepository cacheRepository) : ICommandHandler<Request, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Request request, CancellationToken cancellationToken)
        {
            var tradingZone = await context.TradingZones.FirstOrDefaultAsync(x => x.Id == request.Id && x.CreatedBy == request.UserId, cancellationToken);

            if (tradingZone is null)
            {
                return Result<bool>.Failure(Error.NotFound);
            }

            context.TradingZones.Remove(tradingZone);

            await context.SaveChangesAsync(cancellationToken);
            await cacheRepository.RemoveCache(CacheKeys.TradingZones, cancellationToken);

            return Result<bool>.Success(true);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradingZones);

            group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal user, ISender sender) =>
            {
                Result<bool> result = await sender.Send(new Request(id) with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<bool>>(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Delete a trading zone by ID.")
            .WithDescription("Deletes a trading zone by its ID.")
            .WithTags(Tags.TradingZones)
            .RequireAuthorization("AdminOnly");
        }
    }
}
