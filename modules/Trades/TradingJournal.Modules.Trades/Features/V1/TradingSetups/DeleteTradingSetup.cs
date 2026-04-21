namespace TradingJournal.Modules.Trades.Features.V1.TradingSetups;

public sealed class DeleteTradingSetup
{
    public record Request(int Id, int UserId = 0) : ICommand<Result<bool>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Setup id must be greater than 0.");
        }
    }

    public sealed class Handler(ITradeDbContext context) : ICommandHandler<Request, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Request request, CancellationToken cancellationToken)
        {
            TradingSetup? tradingSetup = await context.TradingSetups
                .FirstOrDefaultAsync(setup => setup.Id == request.Id && setup.CreatedBy == request.UserId, cancellationToken);

            if (tradingSetup is null)
            {
                return Result<bool>.Failure(Error.Create($"Trading setup with id {request.Id} not found."));
            }

            context.TradingSetups.Remove(tradingSetup);

            int deletedRows = await context.SaveChangesAsync(cancellationToken);

            return deletedRows > 0
                ? Result<bool>.Success(true)
                : Result<bool>.Failure(Error.Create("Failed to delete trading setup."));
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradingSetups);

            group.MapDelete("/{id:int}", async (int id, ISender sender) =>
            {
                Result<bool> result = await sender.Send(new Request(id));
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .Produces<Result<bool>>()
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Delete a trading setup flow chart.")
            .WithTags(Tags.TradingSetups)
            .RequireAuthorization();
        }
    }
}