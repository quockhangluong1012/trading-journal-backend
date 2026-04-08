namespace TradingJournal.Modules.Trades.Features.V1.TradingSession;

public sealed class EndTradeSession
{
    public sealed record Request(int Id, DateTime ToTime, string? Duration, string? Note, int UserId = 0) : ICommand<Result<bool>>;

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Id)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Trade session ID cannot be null.")
                .GreaterThan(0).WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Trade session ID must be greater than 0.");

            RuleFor(x => x.ToTime)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("ToTime cannot be null.");

            RuleFor(x => x.Note)
                .Cascade(CascadeMode.Stop)
                .MaximumLength(500).WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Note cannot exceed 500 characters.");
        }
    }

    internal sealed class Handler(ITradeDbContext context) : ICommandHandler<Request, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Request request, CancellationToken cancellationToken)
        {
            var tradeSession = await context.TradingSessions.FindAsync([request.Id], cancellationToken: cancellationToken);

            if (tradeSession == null)
            {
                return Result<bool>.Failure(Error.Create("Trade session not found."));
            }

            tradeSession.ToTime = request.ToTime;
            tradeSession.Note = request.Note;
            tradeSession.Status = TradingSessionStatus.Closed;
            tradeSession.Duration = request.Duration;

            // trade count
            tradeSession.TradeCount = await context.TradeHistories
                .AsNoTracking()
                .Where(x => x.TradingSessionId == request.Id)
                .CountAsync(cancellationToken);

            // trade pnl
            tradeSession.PnL = await context.TradeHistories
                .AsNoTracking()
                .Where(x => x.TradingSessionId == request.Id)
                .SumAsync(x => x.Pnl, cancellationToken);

            int updatedRow = await context.SaveChangesAsync(cancellationToken);

            return updatedRow > 0 ? Result<bool>.Success(true)
                : Result<bool>.Failure(Error.Create("Failed to end trade session."));
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradingSessions);

            group.MapPost("/end", async ([FromBody] Request request, ISender sender) =>
            {
                Result<bool> result = await sender.Send(request);

                return result.IsSuccess ? Results.Ok(result)
                    : Results.BadRequest(result.Errors);
            })
            .Produces<Result<bool>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("End a trade session.")
            .WithDescription("Ends a trade session with the given ID.")
            .WithTags(Tags.TradingSessions)
            .RequireAuthorization();
        }
    }
}