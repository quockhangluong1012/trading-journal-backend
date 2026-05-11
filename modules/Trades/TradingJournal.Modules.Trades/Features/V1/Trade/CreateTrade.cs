using Mapster;
using TradingJournal.Modules.Trades.Services;
using TradingJournal.Shared.CQRS;
using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Trades.Features.V1.Trade;

public sealed class CreateTrade
{
    public record Request(string Asset,
        PositionType Position,
        decimal EntryPrice,
        decimal TargetTier1,
        decimal? TargetTier2,
        decimal? TargetTier3,
        decimal StopLoss,
        string Notes,
        DateTime Date,
        TradeStatus Status,
        decimal? ExitPrice,
        decimal? Pnl,
        DateTime? ClosedDate,
        List<string>? Screenshots,
        List<int>? TradeTechnicalAnalysisTags,
        List<int>? EmotionTags,
        ConfidenceLevel ConfidenceLevel,
        string? PsychologyNotes,
        List<int> TradeHistoryChecklists,
        int TradingZoneId,
        int? TradingSessionId,
        int? TradingSetupId = null,
        PowerOf3Phase? PowerOf3Phase = null,
        DailyBias? DailyBias = null,
        MarketStructure? MarketStructure = null,
        PremiumDiscount? PremiumDiscount = null) : ICommand<Result<int>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Asset)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Asset cannot be null.");

            RuleFor(x => x.Position)
                .Cascade(CascadeMode.Stop)
                .Must(Enum.IsDefined)
                .WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Position must be a valid TradeEnum value.");

            RuleFor(x => x.EntryPrice)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("EntryPrice must be greater than 0.");

            RuleFor(x => x.TargetTier1)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("First Target Tier must be greater than 0.");

            RuleFor(x => x.StopLoss)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Stop Loss must be entered and greater than 0.");

            RuleFor(x => x.Notes)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Must enter notes ( analysis of the trade ).");

            RuleFor(x => x.Date)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Date of the trade must be entered.");

            RuleFor(x => x.Status)
                .Cascade(CascadeMode.Stop)
                .Must(Enum.IsDefined)
                .WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Status must be a valid TradeStatus value.");

            RuleFor(x => x.TradeHistoryChecklists)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Pretrade checklists must be entered.")
                .Must(checklistIds => checklistIds is { Count: > 0 })
                .WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("At least one pretrade checklist must be provided.");

            RuleFor(x => x.TradingZoneId)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Trading Zone must be entered and greater than 0.");

            RuleFor(x => x.TradingSetupId)
                .GreaterThan(0)
                .When(x => x.TradingSetupId.HasValue)
                .WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Trading setup must be greater than 0 when provided.");

            RuleFor(x => x.PowerOf3Phase)
                .IsInEnum()
                .When(x => x.PowerOf3Phase.HasValue)
                .WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Power of 3 phase must be a valid value.");

            RuleFor(x => x.DailyBias)
                .IsInEnum()
                .When(x => x.DailyBias.HasValue)
                .WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Daily bias must be a valid value.");

            RuleFor(x => x.MarketStructure)
                .IsInEnum()
                .When(x => x.MarketStructure.HasValue)
                .WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Market structure must be a valid value.");

            RuleFor(x => x.PremiumDiscount)
                .IsInEnum()
                .When(x => x.PremiumDiscount.HasValue)
                .WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Premium discount must be a valid value.");
        }
    }

    public sealed class Handler(
        ITradeDbContext context,
        IScreenshotService screenshotService,
        IDisciplineEvaluator disciplineEvaluator,
        IHttpContextAccessor httpContextAccessor,
        ISetupProvider setupProvider,
        ICacheRepository cacheRepository) : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            int userId = httpContextAccessor.HttpContext?.User.GetCurrentUserId() ?? 0;
            if (userId <= 0)
            {
                return Result<int>.Failure(Error.Create("Unauthorized."));
            }

            List<int> checklistIds = [.. request.TradeHistoryChecklists.Distinct()];

            int accessibleChecklistCount = await context.PretradeChecklists
                .AsNoTracking()
                .CountAsync(checklist => checklistIds.Contains(checklist.Id) && checklist.ChecklistModel.CreatedBy == userId, cancellationToken);

            if (accessibleChecklistCount != checklistIds.Count)
            {
                return Result<int>.Failure(Error.Create("One or more pretrade checklist items are invalid for the current user."));
            }

            if (request.TradingSetupId.HasValue)
            {
                bool hasSetup = await setupProvider.HasSetupAsync(userId, request.TradingSetupId.Value, cancellationToken);

                if (!hasSetup)
                {
                    return Result<int>.Failure(Error.Create("The selected trading setup is invalid for the current user."));
                }
            }

            try 
            {
                Result<int> result = await context.ExecuteInTransactionAsync(async ct =>
                {
                    TradeHistory tradeHistory = request.Adapt<TradeHistory>();
                    tradeHistory.PowerOf3Phase = request.PowerOf3Phase;
                    tradeHistory.DailyBias = request.DailyBias;
                    tradeHistory.MarketStructure = request.MarketStructure;
                    tradeHistory.PremiumDiscount = request.PremiumDiscount;

                    await disciplineEvaluator.EvaluateAsync(tradeHistory, userId, ct);

                    tradeHistory.TradeTechnicalAnalysisTags = [];
                    tradeHistory.TradeScreenShots = [];
                    tradeHistory.TradeEmotionTags = [];
                    tradeHistory.TradeChecklists = [];

                    await context.TradeHistories.AddAsync(tradeHistory, ct);

                    await context.TradeHistoryChecklist.AddRangeAsync(checklistIds.Select(checklistId => new TradeHistoryChecklist
                    {
                        Id = 0,
                        PretradeChecklistId = checklistId,
                        TradeHistory = tradeHistory
                    }), ct);

                    await context.TradeEmotionTags.AddRangeAsync(request.EmotionTags?.Select(tagId => new TradeEmotionTag
                    {
                        Id = 0,
                        EmotionTagId = tagId,
                        TradeHistory = tradeHistory
                    }) ?? [], ct);

                    List<string> filteredScreenshots = request.Screenshots?.Where(x => !string.IsNullOrEmpty(x)).ToList() ?? [];
                    screenshotService.ValidateScreenshotCount(filteredScreenshots.Count);

                    List<TradeScreenShot> screenshotEntities = [];
                    foreach (string screenshot in filteredScreenshots)
                    {
                        string url = await screenshotService.SaveScreenshotAsync(screenshot, ct);
                        screenshotEntities.Add(new TradeScreenShot
                        {
                            Id = 0,
                            Url = url,
                            TradeHistory = tradeHistory
                        });
                    }

                    await context.TradeScreenShots.AddRangeAsync(screenshotEntities, ct);

                    await context.TradeTechnicalAnalysisTags.AddRangeAsync(request.TradeTechnicalAnalysisTags?.Select(tagId => new TradeTechnicalAnalysisTag
                    {
                        Id = 0,
                        TechnicalAnalysisId = tagId,
                        TradeHistory = tradeHistory
                    }) ?? [], ct);

                    int insertedRow = await context.SaveChangesAsync(ct);

                    return insertedRow > 0
                        ? Result<int>.Success(tradeHistory.Id)
                        : Result<int>.Failure(Error.Create("Failed to create trade history."));
                }, cancellationToken);

                if (result.IsSuccess)
                {
                    await cacheRepository.RemoveCache(CacheKeys.TradesForUser(userId), cancellationToken);
                }

                return result;
            }
            catch (InvalidOperationException ex)
            {
                return Result<int>.Failure(Error.Create(ex.Message));
            }
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradeHistory);

            group.MapPost("/", async ([FromBody] Request request, ClaimsPrincipal user, ISender sender) => {
                Result<int> result = await sender.Send(request);

                return result.IsSuccess ? Results.Created($"{ApiGroup.V1.TradeHistory}/{result.Value}", result) 
                    : Results.BadRequest(result);
            })
            .Produces<Result<int>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Create a new trade history.")
            .WithDescription("Creates a new trade history with the given details.") 
            .WithTags(Tags.TradeHistory)
            .RequireAuthorization();
        }
    }
}
