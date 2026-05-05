namespace TradingJournal.Modules.Trades.Features.V1.Discipline;

public sealed class LogDiscipline
{
    public sealed record Request(
        int DisciplineRuleId,
        int? TradeHistoryId,
        bool WasFollowed,
        string? Notes,
        DateTime Date) : ICommand<Result<int>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.DisciplineRuleId)
                .GreaterThan(0).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Discipline rule ID is required.");

            RuleFor(x => x.Notes)
                .MaximumLength(500).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Notes must not exceed 500 characters.");
        }
    }

    public sealed class Handler(ITradeDbContext context, IHttpContextAccessor http)
        : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            int userId = http.HttpContext?.User.GetCurrentUserId() ?? 0;

            // Verify rule belongs to user
            bool ruleExists = await context.DisciplineRules
                .AsNoTracking()
                .AnyAsync(r => r.Id == request.DisciplineRuleId && r.CreatedBy == userId, cancellationToken);

            if (!ruleExists)
                return Result<int>.Failure(Error.Create("Discipline rule not found."));

            // Verify trade belongs to user (if provided)
            if (request.TradeHistoryId.HasValue)
            {
                bool tradeExists = await context.TradeHistories
                    .AsNoTracking()
                    .AnyAsync(t => t.Id == request.TradeHistoryId.Value && t.CreatedBy == userId, cancellationToken);

                if (!tradeExists)
                    return Result<int>.Failure(Error.Create("Trade not found."));
            }

            DisciplineLog log = new()
            {
                Id = 0,
                DisciplineRuleId = request.DisciplineRuleId,
                TradeHistoryId = request.TradeHistoryId,
                WasFollowed = request.WasFollowed,
                Notes = request.Notes,
                Date = request.Date
            };

            await context.DisciplineLogs.AddAsync(log, cancellationToken);
            int rows = await context.SaveChangesAsync(cancellationToken);

            return rows > 0
                ? Result<int>.Success(log.Id)
                : Result<int>.Failure(Error.Create("Failed to log discipline check."));
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Discipline);

            group.MapPost("/log", async ([FromBody] Request request, ISender sender) =>
            {
                Result<int> result = await sender.Send(request);
                return result.IsSuccess ? Results.Created($"", result) : Results.BadRequest(result);
            })
            .Produces<Result<int>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Log a discipline check.")
            .WithDescription("Records whether a discipline rule was followed for a specific trade or date.")
            .WithTags(Tags.Discipline)
            .RequireAuthorization();
        }
    }
}
