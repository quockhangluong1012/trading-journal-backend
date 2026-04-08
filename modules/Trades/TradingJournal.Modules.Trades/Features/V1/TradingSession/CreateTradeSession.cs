using Mapster;

namespace TradingJournal.Modules.Trades.Features.V1.TradingSession;

public sealed class CreateTradeSession
{
    public record Request(DateTime? FromTime, int UserId = 0) : ICommand<Result<int>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.FromTime)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("FromTime cannot be null.");
        }
    }
    
    public sealed class Handler(ITradeDbContext context) : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            if (request.UserId == 0)
            {
                return Result<int>.Failure(Error.Create("Unauthorized."));
            }

            var tradeSession = request.Adapt<Domain.TradingSession>();
            tradeSession.CreatedBy = request.UserId;

            await context.TradingSessions.AddAsync(tradeSession, cancellationToken);

            int insertedRow = await context.SaveChangesAsync(cancellationToken);

            return insertedRow > 0 ? Result<int>.Success(tradeSession.Id)
                : Result<int>.Failure(Error.Create("Failed to create trade session."));
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradingSessions);

            group.MapPost("/", async (Request request, ISender sender) =>
            {
                Result<int> result = await sender.Send(request);

                return result.IsSuccess ? Results.Created($"/{ApiGroup.V1.TradingSessions}/{result.Value}", result.Value)
                    : Results.BadRequest(result.Errors);
            })
            .Produces<Result<int>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Create a new trade session.")
            .WithDescription("Creates a new trade session with the given details.")
            .WithTags(Tags.TradingSessions)
            .RequireAuthorization();
        }
    }
}