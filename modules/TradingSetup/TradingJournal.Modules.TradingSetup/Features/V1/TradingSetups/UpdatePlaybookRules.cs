namespace TradingJournal.Modules.Setups.Features.V1.TradingSetups;

public sealed class UpdatePlaybookRules
{
    public sealed record Request(
        int SetupId,
        string? EntryRules,
        string? ExitRules,
        string? IdealMarketConditions,
        decimal? RiskPerTrade,
        decimal? TargetRiskReward,
        string? PreferredTimeframes,
        string? PreferredAssets,
        int UserId = 0) : ICommand<Result<bool>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.SetupId)
                .GreaterThan(0)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Setup ID is required.");

            RuleFor(x => x.RiskPerTrade)
                .InclusiveBetween(0.01m, 100m)
                .When(x => x.RiskPerTrade.HasValue)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Risk per trade must be between 0.01% and 100%.");

            RuleFor(x => x.TargetRiskReward)
                .InclusiveBetween(0.1m, 50m)
                .When(x => x.TargetRiskReward.HasValue)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Target R:R must be between 0.1 and 50.");
        }
    }

    public sealed class Handler(ISetupDbContext context) : ICommandHandler<Request, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Request request, CancellationToken cancellationToken)
        {
            if (request.UserId <= 0)
            {
                return Result<bool>.Failure(Error.Create("Current user is required."));
            }

            TradingSetup? setup = await context.TradingSetups
                .FirstOrDefaultAsync(s => s.Id == request.SetupId && s.CreatedBy == request.UserId && !s.IsDisabled,
                    cancellationToken);

            if (setup is null)
            {
                return Result<bool>.Failure(Error.Create("Setup not found."));
            }

            setup.EntryRules = request.EntryRules?.Trim();
            setup.ExitRules = request.ExitRules?.Trim();
            setup.IdealMarketConditions = request.IdealMarketConditions?.Trim();
            setup.RiskPerTrade = request.RiskPerTrade;
            setup.TargetRiskReward = request.TargetRiskReward;
            setup.PreferredTimeframes = request.PreferredTimeframes?.Trim();
            setup.PreferredAssets = request.PreferredAssets?.Trim();

            await context.SaveChangesAsync(cancellationToken);

            return Result<bool>.Success(true);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradingSetups);

            group.MapPut("/{setupId:int}/playbook", async (int setupId, [FromBody] PlaybookRulesPayload payload,
                ClaimsPrincipal user, ISender sender) =>
            {
                var request = new Request(
                    setupId,
                    payload.EntryRules,
                    payload.ExitRules,
                    payload.IdealMarketConditions,
                    payload.RiskPerTrade,
                    payload.TargetRiskReward,
                    payload.PreferredTimeframes,
                    payload.PreferredAssets,
                    user.GetCurrentUserId());

                Result<bool> result = await sender.Send(request);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<bool>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Update playbook rules and metadata for a trading setup.")
            .WithTags(Tags.TradingSetups)
            .RequireAuthorization();
        }
    }
}

public sealed record PlaybookRulesPayload(
    string? EntryRules,
    string? ExitRules,
    string? IdealMarketConditions,
    decimal? RiskPerTrade,
    decimal? TargetRiskReward,
    string? PreferredTimeframes,
    string? PreferredAssets);
