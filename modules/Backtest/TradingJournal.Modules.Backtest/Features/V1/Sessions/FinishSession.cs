namespace TradingJournal.Modules.Backtest.Features.V1.Sessions;

public sealed class FinishSession
{
    public record Request(int SessionId, decimal? ExitPrice) : ICommand<Result>
    {
        public int UserId { get; set; }
    }

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.SessionId)
                .GreaterThan(0).WithMessage("Session ID is required.");

            RuleFor(x => x.ExitPrice)
                .GreaterThan(0).WithMessage("Exit price must be greater than 0.")
                .When(x => x.ExitPrice.HasValue);
        }
    }

    internal sealed class Handler(IBacktestDbContext context) : ICommandHandler<Request, Result>
    {
        public async Task<Result> Handle(Request request, CancellationToken cancellationToken)
        {
            BacktestSession? session = await context.BacktestSessions
                .FirstOrDefaultAsync(s => s.Id == request.SessionId
                                          && s.CreatedBy == request.UserId
                                          && !s.IsDisabled, cancellationToken);

            if (session is null)
                return Result.Failure(Error.Create("Session not found."));

            if (session.Status != BacktestSessionStatus.InProgress)
                return Result.Failure(Error.Create("Only in-progress sessions can be finished."));

            List<BacktestOrder> pendingOrders = await context.BacktestOrders
                .Where(o => o.SessionId == request.SessionId && o.Status == BacktestOrderStatus.Pending)
                .ToListAsync(cancellationToken);

            List<BacktestOrder> activeOrders = await context.BacktestOrders
                .Where(o => o.SessionId == request.SessionId && o.Status == BacktestOrderStatus.Active)
                .ToListAsync(cancellationToken);

            if (activeOrders.Count != 0 && request.ExitPrice is not > 0m)
                return Result.Failure(Error.Create("Exit price is required to close active positions."));

            await context.BeginTransaction();

            try
            {
                foreach (BacktestOrder pendingOrder in pendingOrders)
                {
                    pendingOrder.Status = BacktestOrderStatus.Cancelled;
                }

                decimal exitPrice = request.ExitPrice ?? 0m;

                foreach (BacktestOrder activeOrder in activeOrders)
                {
                    decimal executedExitPrice = activeOrder.Side == BacktestOrderSide.Short
                        ? exitPrice + session.Spread
                        : exitPrice;

                    decimal entryPrice = activeOrder.FilledPrice ?? activeOrder.EntryPrice;
                    decimal pnl = activeOrder.Side switch
                    {
                        BacktestOrderSide.Long => (executedExitPrice - entryPrice) * activeOrder.PositionSize,
                        BacktestOrderSide.Short => (entryPrice - executedExitPrice) * activeOrder.PositionSize,
                        _ => 0m
                    };

                    activeOrder.Status = BacktestOrderStatus.Closed;
                    activeOrder.ExitPrice = executedExitPrice;
                    activeOrder.Pnl = pnl;
                    activeOrder.ClosedAt = session.CurrentTimestamp;

                    session.CurrentBalance += pnl;

                    context.BacktestTradeResults.Add(new BacktestTradeResult
                    {
                        Id = 0,
                        SessionId = session.Id,
                        OrderId = activeOrder.Id,
                        Side = activeOrder.Side,
                        EntryPrice = entryPrice,
                        ExitPrice = executedExitPrice,
                        PositionSize = activeOrder.PositionSize,
                        Pnl = pnl,
                        BalanceAfter = session.CurrentBalance,
                        EntryTime = activeOrder.FilledAt ?? activeOrder.OrderedAt,
                        ExitTime = session.CurrentTimestamp,
                        ExitReason = "Session Finished"
                    });
                }

                session.EndDate = session.CurrentTimestamp;
                session.Status = BacktestSessionStatus.Completed;

                await context.SaveChangesAsync(cancellationToken);
                await context.CommitTransaction();
                return Result.Success();
            }
            catch
            {
                await context.RollbackTransaction();
                throw;
            }
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Sessions);

            group.MapPost("/{sessionId:int}/finish", async (int sessionId, decimal? exitPrice, ClaimsPrincipal user, ISender sender) =>
            {
                Result result = await sender.Send(new Request(sessionId, exitPrice) with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result);
            })
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Finish an in-progress backtest session early.")
            .WithDescription("Closes active positions at the supplied market price, cancels pending orders, and marks the session as completed.")
            .WithTags(Tags.BacktestSessions)
            .RequireAuthorization();
        }
    }
}