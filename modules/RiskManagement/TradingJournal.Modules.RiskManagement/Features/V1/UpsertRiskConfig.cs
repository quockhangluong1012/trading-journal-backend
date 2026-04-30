namespace TradingJournal.Modules.RiskManagement.Features.V1;

public sealed class UpsertRiskConfig
{
    internal sealed record Command(
        decimal DailyLossLimitPercent,
        decimal WeeklyDrawdownCapPercent,
        decimal RiskPerTradePercent,
        int MaxOpenPositions,
        int MaxCorrelatedPositions,
        decimal AccountBalance,
        int UserId = 0) : ICommand<Result>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.DailyLossLimitPercent)
                .GreaterThan(0).WithMessage("Daily loss limit must be greater than 0.")
                .LessThanOrEqualTo(100).WithMessage("Daily loss limit cannot exceed 100%.");

            RuleFor(x => x.WeeklyDrawdownCapPercent)
                .GreaterThan(0).WithMessage("Weekly drawdown cap must be greater than 0.")
                .LessThanOrEqualTo(100).WithMessage("Weekly drawdown cap cannot exceed 100%.");

            RuleFor(x => x.RiskPerTradePercent)
                .GreaterThan(0).WithMessage("Risk per trade must be greater than 0.")
                .LessThanOrEqualTo(50).WithMessage("Risk per trade cannot exceed 50%.");

            RuleFor(x => x.MaxOpenPositions)
                .GreaterThan(0).WithMessage("Max open positions must be at least 1.")
                .LessThanOrEqualTo(50).WithMessage("Max open positions cannot exceed 50.");

            RuleFor(x => x.MaxCorrelatedPositions)
                .GreaterThan(0).WithMessage("Max correlated positions must be at least 1.")
                .LessThanOrEqualTo(20).WithMessage("Max correlated positions cannot exceed 20.");

            RuleFor(x => x.AccountBalance)
                .GreaterThan(0).WithMessage("Account balance must be greater than 0.");
        }
    }

    internal sealed class Handler(IRiskDbContext context) : ICommandHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            RiskConfig? existing = await context.RiskConfigs
                .FirstOrDefaultAsync(x => x.CreatedBy == request.UserId, cancellationToken);

            if (existing is null)
            {
                existing = new RiskConfig
                {
                    Id = default!,
                    DailyLossLimitPercent = request.DailyLossLimitPercent,
                    WeeklyDrawdownCapPercent = request.WeeklyDrawdownCapPercent,
                    RiskPerTradePercent = request.RiskPerTradePercent,
                    MaxOpenPositions = request.MaxOpenPositions,
                    MaxCorrelatedPositions = request.MaxCorrelatedPositions,
                    AccountBalance = request.AccountBalance,
                };
                context.RiskConfigs.Add(existing);
            }
            else
            {
                existing.DailyLossLimitPercent = request.DailyLossLimitPercent;
                existing.WeeklyDrawdownCapPercent = request.WeeklyDrawdownCapPercent;
                existing.RiskPerTradePercent = request.RiskPerTradePercent;
                existing.MaxOpenPositions = request.MaxOpenPositions;
                existing.MaxCorrelatedPositions = request.MaxCorrelatedPositions;
                existing.AccountBalance = request.AccountBalance;
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/risk");

            group.MapPut("/config", async (Command command, ClaimsPrincipal user, ISender sender) =>
            {
                Result result = await sender.Send(command with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Create or update risk configuration.")
            .WithDescription("Creates or updates the user's risk management settings.")
            .WithTags(Tags.RiskManagement)
            .RequireAuthorization();
        }
    }
}
