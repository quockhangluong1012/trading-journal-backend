using Mapster;
using Microsoft.AspNetCore.Hosting;
using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Trades.Features.V1.Trade;

public sealed class CreateTrade
{
    public record Request(string Asset,
        PositionType Position,
        double EntryPrice,
        double TargetTier1,
        double? TargetTier2,
        double? TargetTier3,
        double StopLoss,
        string Notes,
        DateTime Date,
        TradeStatus Status,
        double? ExitPrice,
        double? Pnl,
        DateTime? ClosedDate,
        List<string>? Screenshots,
        List<int>? TradeTechnicalAnalysisTags,
        List<int>? EmotionTags,
        ConfidenceLevel ConfidenceLevel,
        string? PsychologyNotes,
        List<int> TradeHistoryChecklists,
        int TradingZoneId,
        int? TradingSessionId) : ICommand<Result<int>>;

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
        }
    }

    public sealed class Handler(ITradeDbContext context, IWebHostEnvironment env, IHttpContextAccessor httpContextAccessor) : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            try 
            {
                await context.BeginTransaction();

                int userId = httpContextAccessor.HttpContext?.User.GetCurrentUserId() ?? 0;
                if (userId <= 0)
                {
                    await context.RollbackTransaction();
                    return Result<int>.Failure(Error.Create("Unauthorized."));
                }

                List<int> checklistIds = [.. request.TradeHistoryChecklists.Distinct()];

                int accessibleChecklistCount = await context.PretradeChecklists
                    .AsNoTracking()
                    .CountAsync(checklist => checklistIds.Contains(checklist.Id) && checklist.ChecklistModel.CreatedBy == userId, cancellationToken);

                if (accessibleChecklistCount != checklistIds.Count)
                {
                    await context.RollbackTransaction();
                    return Result<int>.Failure(Error.Create("One or more pretrade checklist items are invalid for the current user."));
                }

                TradeHistory tradeHistory = request.Adapt<TradeHistory>();

                if (userId > 0)
                {
                    await EvaluateDisciplineRules(tradeHistory, userId, context, cancellationToken);
                }

                tradeHistory.TradeTechnicalAnalysisTags = [];
                tradeHistory.TradeScreenShots = [];
                tradeHistory.TradeEmotionTags = [];
                tradeHistory.TradeChecklists = [];

                await context.TradeHistories.AddAsync(tradeHistory, cancellationToken);

                await context.TradeHistoryChecklist.AddRangeAsync(checklistIds.Select(checklistId => new TradeHistoryChecklist
                {
                    Id = 0,
                    PretradeChecklistId = checklistId,
                    TradeHistory = tradeHistory
                }), cancellationToken);

                await context.TradeEmotionTags.AddRangeAsync(request.EmotionTags?.Select(tagId => new TradeEmotionTag
                {
                    Id = 0,
                    EmotionTagId = tagId,
                    TradeHistory = tradeHistory
                }) ?? [], cancellationToken);

                List<string> filteredScreenshots = request.Screenshots?.Where(x => !string.IsNullOrEmpty(x)).ToList() ?? [];

                List<TradeScreenShot> screenshotEntities = [];
                foreach (string screenshot in filteredScreenshots)
                {
                    string url = SaveBase64ToFile(screenshot);
                    screenshotEntities.Add(new TradeScreenShot
                    {
                        Id = 0,
                        Url = url,
                        TradeHistory = tradeHistory
                    });
                }
                
                await context.TradeScreenShots.AddRangeAsync(screenshotEntities, cancellationToken);

                await context.TradeTechnicalAnalysisTags.AddRangeAsync(request.TradeTechnicalAnalysisTags?.Select(tagId => new TradeTechnicalAnalysisTag
                {
                    Id = 0,
                    TechnicalAnalysisId = tagId,
                    TradeHistory = tradeHistory
                }) ?? [], cancellationToken);

                int insertedRow = await context.SaveChangesAsync(cancellationToken);

                await context.CommitTransaction();

                return insertedRow > 0 ? Result<int>.Success(tradeHistory.Id)
                    : Result<int>.Failure(Error.Create("Failed to create trade history."));
            }
            catch (Exception ex)
            {
                await context.RollbackTransaction();
                return Result<int>.Failure(Error.Create(ex.Message));
            }
        }

        private string SaveBase64ToFile(string base64String)
        {
            // Strip the data URI prefix if present (e.g., "data:image/png;base64,")
            if (base64String.Contains(','))
            {
                base64String = base64String[(base64String.IndexOf(',') + 1)..];
            }

            byte[] imageBytes = Convert.FromBase64String(base64String);

            var screenshotDir = Path.Combine(env.ContentRootPath, "wwwroot", "screenshots");
            if (!Directory.Exists(screenshotDir))
            {
                Directory.CreateDirectory(screenshotDir);
            }

            var fileName = Guid.NewGuid().ToString() + ".png";
            var filePath = Path.Combine(screenshotDir, fileName);

            File.WriteAllBytes(filePath, imageBytes);

            HttpContext? httpContext = httpContextAccessor.HttpContext;

            if (httpContext != null)
            {
                return $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/screenshots/{fileName}";
            }

            return $"/screenshots/{fileName}";
        }

        private async Task EvaluateDisciplineRules(TradeHistory tradeHistory, int userId, ITradeDbContext dbContext, CancellationToken cancellationToken)
        {
            var profile = await dbContext.TradingProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.CreatedBy == userId, cancellationToken);
            
            if (profile == null || !profile.IsDisciplineEnabled) return;

            List<string> brokenReasons = [];

            // Max Trades Per Day
            if (profile.MaxTradesPerDay.HasValue && profile.MaxTradesPerDay > 0)
            {
                var today = DateTime.UtcNow.Date;
                var tradesToday = await dbContext.TradeHistories
                    .AsNoTracking()
                    .CountAsync(x => x.CreatedBy == userId && x.CreatedDate >= today, cancellationToken);

                if (tradesToday >= profile.MaxTradesPerDay.Value)
                {
                    brokenReasons.Add($"Exceeded max trades per day ({profile.MaxTradesPerDay.Value}).");
                }
            }

            // Max Consecutive Losses
            if (profile.MaxConsecutiveLosses.HasValue && profile.MaxConsecutiveLosses > 0)
            {
                var recentTrades = await dbContext.TradeHistories
                    .AsNoTracking()
                    .Where(x => x.CreatedBy == userId && x.Status == TradeStatus.Closed)
                    .OrderByDescending(x => x.ClosedDate ?? x.CreatedDate)
                    .Take(profile.MaxConsecutiveLosses.Value + 1)
                    .ToListAsync(cancellationToken);

                int consecutiveLosses = 0;
                foreach (var trade in recentTrades)
                {
                    if (trade.Pnl < 0 || trade.TradingResult == "Loss")
                    {
                        consecutiveLosses++;
                    }
                    else
                    {
                        break;
                    }
                }

                if (consecutiveLosses >= profile.MaxConsecutiveLosses.Value)
                {
                    brokenReasons.Add($"Exceeded max consecutive losses ({profile.MaxConsecutiveLosses.Value}).");
                }
            }

            if (brokenReasons.Count > 0)
            {
                tradeHistory.IsRuleBroken = true;
                tradeHistory.RuleBreakReason = string.Join("; ", brokenReasons);
            }
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradeHistory);

            group.MapPost("/", async ([FromBody] Request request, ISender sender) => {
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
