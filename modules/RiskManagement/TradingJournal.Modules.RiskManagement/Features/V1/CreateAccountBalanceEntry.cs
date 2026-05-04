namespace TradingJournal.Modules.RiskManagement.Features.V1;

public sealed class CreateAccountBalanceEntry
{
    internal sealed record Command(
        BalanceEntryType EntryType,
        decimal Amount,
        string? Notes,
        DateTimeOffset EntryDate,
        int UserId = 0) : ICommand<Result<int>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.EntryType).IsInEnum().WithMessage("Invalid entry type.");
            RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Amount must be greater than 0.");
            RuleFor(x => x.EntryDate).NotEmpty().WithMessage("Entry date is required.");
        }
    }

    internal sealed class Handler(IRiskDbContext context) : ICommandHandler<Command, Result<int>>
    {
        public async Task<Result<int>> Handle(Command request, CancellationToken cancellationToken)
        {
            // Get the latest balance for this user
            AccountBalanceEntry? latest = await context.AccountBalanceEntries
                .Where(x => x.CreatedBy == request.UserId)
                .OrderByDescending(x => x.EntryDate)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            decimal currentBalance = latest?.BalanceAfter ?? 0;

            decimal newBalance = request.EntryType switch
            {
                BalanceEntryType.Withdrawal => currentBalance - request.Amount,
                _ => currentBalance + request.Amount // Deposit, InitialDeposit, Adjustment all add
            };

            var entry = new AccountBalanceEntry
            {
                Id = default!,
                EntryType = request.EntryType,
                Amount = request.Amount,
                BalanceAfter = newBalance,
                Notes = request.Notes,
                EntryDate = request.EntryDate,
            };

            context.AccountBalanceEntries.Add(entry);

            // Also update the RiskConfig account balance
            RiskConfig? config = await context.RiskConfigs
                .FirstOrDefaultAsync(x => x.CreatedBy == request.UserId, cancellationToken);

            if (config is not null)
            {
                config.AccountBalance = newBalance;
            }
            else
            {
                config = new RiskConfig
                {
                    Id = default!,
                    AccountBalance = newBalance,
                };
                context.RiskConfigs.Add(config);
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result<int>.Success(entry.Id);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/risk");

            group.MapPost("/account-balance", async (Command command, ClaimsPrincipal user, ISender sender) =>
            {
                Result<int> result = await sender.Send(command with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result<int>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Create account balance entry.")
            .WithDescription("Records a deposit, withdrawal, or adjustment to the account balance.")
            .WithTags(Tags.RiskManagement)
            .RequireAuthorization();
        }
    }
}
