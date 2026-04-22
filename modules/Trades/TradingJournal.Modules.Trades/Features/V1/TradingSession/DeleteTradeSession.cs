namespace TradingJournal.Modules.Trades.Features.V1.TradingSession;

public class DeleteTradeSession
{
    public sealed record Request(int Id, int UserId = 0) : ICommand<Result<bool>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Id)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Trade session ID cannot be null.")
                .GreaterThan(0).WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Trade session ID must be greater than 0.");
        }
    }

    public sealed class Handler(ITradeDbContext context) : ICommandHandler<Request, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Request request, CancellationToken cancellationToken)
        {
            var tradeSession = await context.TradingSessions
                .FirstOrDefaultAsync(x => x.Id == request.Id && x.CreatedBy == request.UserId, cancellationToken: cancellationToken);

            if (tradeSession == null)
            {
                return Result<bool>.Failure(Error.Create("Trade session not found."));
            }

            context.TradingSessions.Remove(tradeSession);

            int deletedRow = await context.SaveChangesAsync(cancellationToken);

            return deletedRow > 0 ? Result<bool>.Success(true)
                : Result<bool>.Failure(Error.Create("Failed to delete trade session."));
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradingSessions);

            group.MapDelete("/{id}", async (int id, ClaimsPrincipal user, ISender sender) =>
            {
                Result<bool> result = await sender.Send(new Request(id) with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result)
                    : Results.BadRequest(result.Errors);
            })
            .Produces<Result<bool>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Delete a trade session.")
            .WithDescription("Deletes a trade session with the given ID.")
            .WithTags(Tags.TradingSessions)
            .RequireAuthorization();
        }
    }
}