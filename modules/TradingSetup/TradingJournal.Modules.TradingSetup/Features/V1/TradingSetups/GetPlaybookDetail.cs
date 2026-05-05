namespace TradingJournal.Modules.Setups.Features.V1.TradingSetups;

public sealed class GetPlaybookDetail
{
    public sealed record Request(int SetupId, int UserId = 0)
        : ICommand<Result<PlaybookDetailViewModel>>;

    public sealed class Handler(ISetupDbContext context) : ICommandHandler<Request, Result<PlaybookDetailViewModel>>
    {
        public async Task<Result<PlaybookDetailViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            if (request.UserId <= 0)
            {
                return Result<PlaybookDetailViewModel>.Failure(Error.Create("Current user is required."));
            }

            PlaybookDetailViewModel? playbook = await context.TradingSetups
                .AsNoTracking()
                .Where(s => s.Id == request.SetupId && s.CreatedBy == request.UserId && !s.IsDisabled)
                .Select(s => new PlaybookDetailViewModel(
                    s.Id,
                    s.Name,
                    s.Description,
                    (int)s.Status,
                    s.EntryRules,
                    s.ExitRules,
                    s.IdealMarketConditions,
                    s.RiskPerTrade,
                    s.TargetRiskReward,
                    s.PreferredTimeframes,
                    s.PreferredAssets,
                    s.RetiredReason,
                    s.RetiredDate,
                    s.CreatedDate,
                    s.UpdatedDate ?? s.CreatedDate))
                .FirstOrDefaultAsync(cancellationToken);

            if (playbook is null)
            {
                return Result<PlaybookDetailViewModel>.Failure(Error.Create("Setup not found."));
            }

            return Result<PlaybookDetailViewModel>.Success(playbook);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradingSetups);

            group.MapGet("/{setupId:int}/playbook", async (int setupId, ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request(setupId, user.GetCurrentUserId()));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<PlaybookDetailViewModel>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get playbook detail (rules, conditions, metadata) for a trading setup.")
            .WithTags(Tags.TradingSetups)
            .RequireAuthorization();
        }
    }
}

public sealed record PlaybookDetailViewModel(
    int Id,
    string Name,
    string? Description,
    int Status,
    string? EntryRules,
    string? ExitRules,
    string? IdealMarketConditions,
    decimal? RiskPerTrade,
    decimal? TargetRiskReward,
    string? PreferredTimeframes,
    string? PreferredAssets,
    string? RetiredReason,
    DateTime? RetiredDate,
    DateTime CreatedAt,
    DateTime LastUpdatedAt);
