using Mapster;
using Microsoft.AspNetCore.Hosting;

namespace TradingJournal.Modules.Trades.Features.V1.Trade;

public sealed class UpdateTrade
{
    public record Request(
        int Id,
        string Asset,
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
        int UserId = 0) : ICommand<Result<bool>>;

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
                .Must(checklistIds => checklistIds != null && checklistIds.Count > 0)
                .WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("At least one pretrade checklist must be provided.");

            RuleFor(x => x.TradingZoneId)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Trading Zone must be entered and greater than 0.");
        }
    }

    public sealed class Handler(ITradeDbContext context, IWebHostEnvironment env) : ICommandHandler<Request, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Request request, CancellationToken cancellationToken)
        {
            TradeHistory? tradeHistory = await context.TradeHistories
                    .Include(th => th.TradeScreenShots)
                    .Include(th => th.TradeEmotionTags)
                    .Include(th => th.TradeChecklists)
                    .Include(x => x.TradeTechnicalAnalysisTags)
                    .FirstOrDefaultAsync(th => th.Id == request.Id && th.CreatedBy == request.UserId, cancellationToken);

                if (tradeHistory == null)
                {
                    return Result<bool>.Failure(Error.NotFound);
                }

                List<int> checklistIds = [.. request.TradeHistoryChecklists.Distinct()];

                int accessibleChecklistCount = await context.PretradeChecklists
                    .AsNoTracking()
                    .CountAsync(checklist => checklistIds.Contains(checklist.Id) && checklist.ChecklistModel.CreatedBy == request.UserId, cancellationToken);

                if (accessibleChecklistCount != checklistIds.Count)
                {
                    return Result<bool>.Failure(Error.Create("One or more pretrade checklist items are invalid for the current user."));
                }

                tradeHistory.Asset = request.Asset;
                tradeHistory.Position = request.Position;
                tradeHistory.EntryPrice = request.EntryPrice;
                tradeHistory.TargetTier1 = request.TargetTier1;
                tradeHistory.TargetTier2 = request.TargetTier2;
                tradeHistory.TargetTier3 = request.TargetTier3;
                tradeHistory.StopLoss = request.StopLoss;
                tradeHistory.Notes = request.Notes;
                tradeHistory.Date = request.Date;
                tradeHistory.Status = request.Status;
                tradeHistory.ExitPrice = request.ExitPrice;
                tradeHistory.Pnl = request.Pnl;
                tradeHistory.ClosedDate = request.ClosedDate;
                tradeHistory.ConfidenceLevel = request.ConfidenceLevel;
                tradeHistory.TradingZoneId = request.TradingZoneId;
                tradeHistory.TradingSessionId = request.TradingSessionId;

                #region remove all existing emotion tags, pretrade checklists, and technical analysis tags
                context.TradeEmotionTags.RemoveRange(tradeHistory.TradeEmotionTags ?? []);
                context.TradeHistoryChecklist.RemoveRange(tradeHistory.TradeChecklists);
                context.TradeTechnicalAnalysisTags.RemoveRange(tradeHistory.TradeTechnicalAnalysisTags);

                await context.TradeHistoryChecklist.AddRangeAsync(checklistIds.Select(checklistId => new TradeHistoryChecklist
                {
                    Id = 0,
                    TradeHistoryId = tradeHistory.Id,
                    PretradeChecklistId = checklistId
                }), cancellationToken);

                await context.TradeEmotionTags.AddRangeAsync(request.EmotionTags?.Select(tagId => new TradeEmotionTag
                {
                    Id = 0,
                    TradeHistoryId = tradeHistory.Id,
                    EmotionTagId = tagId
                }) ?? [], cancellationToken);

                await context.TradeTechnicalAnalysisTags.AddRangeAsync(request.TradeTechnicalAnalysisTags?.Select(tagId => new TradeTechnicalAnalysisTag
                {
                    Id = 0,
                    TradeHistoryId = tradeHistory.Id,
                    TechnicalAnalysisId = tagId
                }) ?? [], cancellationToken);
                #endregion

                #region handle screenshots: keep existing URLs, save new base64, delete removed files
                List<string> incomingScreenshots = request.Screenshots?.Where(x => !string.IsNullOrEmpty(x)).ToList() ?? [];

                // Separate incoming into existing URLs (kept) vs new base64 (to save)
                List<string> incomingUrls = incomingScreenshots.Where(s => !IsBase64Image(s)).ToList();
                List<string> newBase64Screenshots = incomingScreenshots.Where(IsBase64Image).ToList();

                // Find screenshots to delete (old URLs not in the incoming list)
                List<TradeScreenShot> screenshotsToDelete = [.. tradeHistory.TradeScreenShots.Where(existing => !incomingUrls.Contains(existing.Url))];

                // Delete physical files for removed screenshots
                foreach (var screenshot in screenshotsToDelete)
                {
                    DeleteScreenshotFile(screenshot.Url);
                }
                context.TradeScreenShots.RemoveRange(screenshotsToDelete);

                // Save new base64 screenshots as files
                foreach (string base64 in newBase64Screenshots)
                {
                    string url = SaveBase64ToFile(base64);
                    await context.TradeScreenShots.AddAsync(new TradeScreenShot
                    {
                        Id = 0,
                        TradeHistoryId = tradeHistory.Id,
                        Url = url
                    }, cancellationToken);
                }
                #endregion

            await context.SaveChangesAsync(cancellationToken);

            return Result<bool>.Success(true);
        }

        private static bool IsBase64Image(string value)
        {
            return value.StartsWith("data:image", StringComparison.OrdinalIgnoreCase)
                || (!value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    && !value.StartsWith("/", StringComparison.Ordinal));
        }

        private string SaveBase64ToFile(string base64String)
        {
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

            return $"/screenshots/{fileName}";
        }

        private void DeleteScreenshotFile(string url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("/screenshots/"))
                return;

            var filePath = Path.Combine(env.ContentRootPath, "wwwroot", url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradeHistory);

            group.MapPut("/", async ([FromBody] Request request, ClaimsPrincipal user, ISender sender) => {
                Result<bool> result = await sender.Send(request with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<bool>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Update an existing trade history.")
            .WithDescription("Updates an existing trade history with the given details.")
            .WithTags(Tags.TradeHistory)
            .RequireAuthorization();
        }
    }
}