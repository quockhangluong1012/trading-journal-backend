namespace TradingJournal.Modules.RiskManagement.Features.V1;

public sealed class GetAccountBalanceHistory
{
    internal sealed record Request(int UserId = 0) : IQuery<Result<List<AccountBalanceViewModel>>>;

    internal sealed record AccountBalanceViewModel(
        int Id,
        string EntryType,
        decimal Amount,
        decimal BalanceAfter,
        string? Notes,
        DateTime EntryDate);

    internal sealed class Handler(IRiskDbContext context) : IQueryHandler<Request, Result<List<AccountBalanceViewModel>>>
    {
        public async Task<Result<List<AccountBalanceViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            List<AccountBalanceViewModel> entries = await context.AccountBalanceEntries
                .AsNoTracking()
                .Where(x => x.CreatedBy == request.UserId)
                .OrderByDescending(x => x.EntryDate)
                .ThenByDescending(x => x.Id)
                .Select(x => new AccountBalanceViewModel(
                    x.Id,
                    x.EntryType.ToString(),
                    x.Amount,
                    x.BalanceAfter,
                    x.Notes,
                    x.EntryDate))
                .ToListAsync(cancellationToken);

            return Result<List<AccountBalanceViewModel>>.Success(entries);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/risk");

            group.MapGet("/account-balance", async (ClaimsPrincipal user, ISender sender) =>
            {
                Result<List<AccountBalanceViewModel>> result = await sender.Send(new Request() with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result<List<AccountBalanceViewModel>>>(StatusCodes.Status200OK)
            .WithSummary("Get account balance history.")
            .WithDescription("Retrieves the user's account balance history including deposits and withdrawals.")
            .WithTags(Tags.RiskManagement)
            .RequireAuthorization();
        }
    }
}
