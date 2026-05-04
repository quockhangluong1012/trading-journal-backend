using TradingJournal.Modules.Trades.Features.V1.Review;
using TradingJournal.Shared.Common;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Trades.Features.V1.ReviewWizard;

public sealed class GetWizardData
{
    public sealed class Request : IQuery<Result<ReviewWizardDataViewModel>>, IUserAwareRequest
    {
        public ReviewPeriodType PeriodType { get; set; }
        public DateTimeOffset PeriodStart { get; set; }
        public int UserId { get; set; }
    }

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.PeriodType)
                .Must(Enum.IsDefined).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("PeriodType must be a valid value.");
        }
    }

    public sealed class Handler(
        IReviewSnapshotBuilder snapshotBuilder,
        ITradeDbContext context) : IQueryHandler<Request, Result<ReviewWizardDataViewModel>>
    {
        public async Task<Result<ReviewWizardDataViewModel>> Handle(
            Request request, CancellationToken cancellationToken)
        {
            // Build current period snapshot
            ReviewPeriodBounds currentBounds = ReviewPeriodCalculator.GetBounds(request.PeriodType, request.PeriodStart);
            ReviewSnapshot currentSnapshot = await snapshotBuilder.BuildAsync(
                request.PeriodType, request.PeriodStart, request.UserId, cancellationToken);

            // Build previous period snapshot for comparison
            DateTimeOffset previousRef = GetPreviousPeriodStart(request.PeriodType, currentBounds.Start);
            ReviewSnapshot previousSnapshot = await snapshotBuilder.BuildAsync(
                request.PeriodType, previousRef, request.UserId, cancellationToken);

            // Build best/worst trades
            List<WizardTradeHighlight> bestTrades = currentSnapshot.Trades
                .OrderByDescending(t => t.Pnl)
                .Take(3)
                .Select(MapTradeHighlight)
                .ToList();

            List<WizardTradeHighlight> worstTrades = currentSnapshot.Trades
                .OrderBy(t => t.Pnl)
                .Take(3)
                .Select(MapTradeHighlight)
                .ToList();

            // Emotion distribution
            List<WizardDistributionItem> emotionDistribution = BuildDistribution(
                currentSnapshot.Trades.SelectMany(t => t.EmotionTags));

            // Confidence distribution
            List<WizardDistributionItem> confidenceDistribution = currentSnapshot.Trades
                .GroupBy(t => t.ConfidenceLevel.ToString())
                .Select(g => new WizardDistributionItem
                {
                    Label = g.Key,
                    Count = g.Count(),
                    Percentage = currentSnapshot.Trades.Count > 0
                        ? Math.Round((decimal)g.Count() / currentSnapshot.Trades.Count * 100, 1)
                        : 0
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            // Discipline summary
            WizardDisciplineSummary discipline = await BuildDisciplineSummaryAsync(
                request.UserId, currentBounds, cancellationToken);

            // Load pending action items from previous reviews
            List<ReviewActionItemViewModel> pendingActionItems = await context.ReviewActionItems
                .AsNoTracking()
                .Include(ai => ai.TradingReview)
                .Where(ai => ai.CreatedBy == request.UserId)
                .Where(ai => ai.Status == ActionItemStatus.Open || ai.Status == ActionItemStatus.InProgress)
                .OrderByDescending(ai => ai.Priority)
                .ThenByDescending(ai => ai.CreatedDate)
                .Take(10)
                .Select(ai => new ReviewActionItemViewModel
                {
                    Id = ai.Id,
                    TradingReviewId = ai.TradingReviewId,
                    Title = ai.Title,
                    Description = ai.Description,
                    Priority = (int)ai.Priority,
                    Status = (int)ai.Status,
                    Category = (int)ai.Category,
                    DueDate = ai.DueDate,
                    CompletedDate = ai.CompletedDate,
                    CompletionNotes = ai.CompletionNotes,
                    CreatedDate = ai.CreatedDate,
                })
                .ToListAsync(cancellationToken);

            // Load existing wizard review if any
            TradingReviewViewModel? existingReview = await LoadExistingReviewAsync(
                request.UserId, request.PeriodType, currentBounds, cancellationToken);

            // Calculate review streak
            int streak = await CalculateReviewStreakAsync(
                request.UserId, request.PeriodType, currentBounds.Start, cancellationToken);

            ReviewWizardDataViewModel result = new()
            {
                Current = MapPeriodMetrics(currentSnapshot, "Current Period"),
                Previous = previousSnapshot.Trades.Count > 0
                    ? MapPeriodMetrics(previousSnapshot, "Previous Period")
                    : null,
                BestTrades = bestTrades,
                WorstTrades = worstTrades,
                EmotionDistribution = emotionDistribution,
                ConfidenceDistribution = confidenceDistribution,
                Discipline = discipline,
                PendingActionItems = pendingActionItems,
                ExistingReview = existingReview,
                ReviewStreak = streak,
            };

            return Result<ReviewWizardDataViewModel>.Success(result);
        }

        private static DateTimeOffset GetPreviousPeriodStart(ReviewPeriodType periodType, DateTimeOffset currentStart)
        {
            return periodType switch
            {
                ReviewPeriodType.Daily => currentStart.AddDays(-1),
                ReviewPeriodType.Weekly => currentStart.AddDays(-7),
                ReviewPeriodType.Monthly => currentStart.AddMonths(-1),
                ReviewPeriodType.Quarterly => currentStart.AddMonths(-3),
                _ => currentStart.AddDays(-7),
            };
        }

        private static WizardTradeHighlight MapTradeHighlight(ReviewTradeInsight trade) => new()
        {
            TradeId = trade.TradeId,
            Asset = trade.Asset,
            Position = trade.Position.ToString(),
            Pnl = trade.Pnl,
            OpenDate = trade.OpenDate,
            ClosedDate = trade.ClosedDate,
            EntryPrice = trade.EntryPrice,
            ExitPrice = trade.ExitPrice,
            IsRuleBroken = trade.IsRuleBroken,
            Notes = trade.Notes,
            EmotionTags = [.. trade.EmotionTags],
        };

        private static List<WizardDistributionItem> BuildDistribution(IEnumerable<string> values)
        {
            List<string> allValues = [.. values.Where(v => !string.IsNullOrWhiteSpace(v))];
            int total = allValues.Count;

            return allValues
                .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
                .Select(g => new WizardDistributionItem
                {
                    Label = g.Key,
                    Count = g.Count(),
                    Percentage = total > 0 ? Math.Round((decimal)g.Count() / total * 100, 1) : 0,
                })
                .OrderByDescending(x => x.Count)
                .ToList();
        }

        private static WizardPeriodMetrics MapPeriodMetrics(ReviewSnapshot snapshot, string label)
        {
            ReviewSnapshotMetrics m = snapshot.Metrics;
            decimal totalWinAmount = snapshot.Trades.Where(t => t.Pnl > 0).Sum(t => t.Pnl);
            decimal totalLossAmount = Math.Abs(snapshot.Trades.Where(t => t.Pnl <= 0).Sum(t => t.Pnl));

            return new WizardPeriodMetrics
            {
                PeriodLabel = label,
                PeriodStart = snapshot.PeriodStart,
                PeriodEnd = snapshot.PeriodEnd,
                TotalTrades = m.TotalTrades,
                Wins = m.Wins,
                Losses = m.Losses,
                TotalPnl = m.TotalPnl,
                WinRate = m.WinRate,
                AverageWin = m.AverageWin,
                AverageLoss = m.AverageLoss,
                BestTradePnl = m.BestTradePnl,
                WorstTradePnl = m.WorstTradePnl,
                LongTrades = m.LongTrades,
                ShortTrades = m.ShortTrades,
                RuleBreakTrades = m.RuleBreakTrades,
                HighConfidenceTrades = m.HighConfidenceTrades,
                TopAsset = m.TopAsset,
                PrimaryTradingZone = m.PrimaryTradingZone,
                DominantEmotion = m.DominantEmotion,
                ProfitFactor = totalLossAmount > 0 ? Math.Round(totalWinAmount / totalLossAmount, 2) : 0,
                Expectancy = m.TotalTrades > 0 ? Math.Round(m.TotalPnl / m.TotalTrades, 2) : 0,
            };
        }

        private async Task<WizardDisciplineSummary> BuildDisciplineSummaryAsync(
            int userId, ReviewPeriodBounds bounds, CancellationToken cancellationToken)
        {
            List<DisciplineLog> logs = await context.DisciplineLogs
                .AsNoTracking()
                .Include(dl => dl.DisciplineRule)
                .Where(dl => dl.CreatedBy == userId)
                .Where(dl => dl.Date >= bounds.Start && dl.Date <= bounds.End)
                .ToListAsync(cancellationToken);

            int followed = logs.Count(l => l.WasFollowed);
            int broken = logs.Count(l => !l.WasFollowed);

            return new WizardDisciplineSummary
            {
                TotalRuleChecks = logs.Count,
                RulesFollowed = followed,
                RulesBroken = broken,
                ComplianceRate = logs.Count > 0 ? Math.Round((decimal)followed / logs.Count * 100, 1) : 100,
                RuleBreakdowns = [.. logs
                    .GroupBy(l => l.DisciplineRule.Name)
                    .Select(g => new WizardRuleBreakdown
                    {
                        RuleName = g.Key,
                        TimesFollowed = g.Count(l => l.WasFollowed),
                        TimesBroken = g.Count(l => !l.WasFollowed),
                    })
                    .OrderByDescending(r => r.TimesBroken)],
            };
        }

        private async Task<TradingReviewViewModel?> LoadExistingReviewAsync(
            int userId, ReviewPeriodType periodType, ReviewPeriodBounds bounds,
            CancellationToken cancellationToken)
        {
            TradingReview? review = await context.TradingReviews
                .AsNoTracking()
                .Include(r => r.ActionItems)
                .Where(r => r.CreatedBy == userId)
                .Where(r => r.PeriodType == periodType)
                .Where(r => r.PeriodStart == bounds.Start)
                .FirstOrDefaultAsync(cancellationToken);

            if (review is null) return null;

            return MapReviewToViewModel(review);
        }

        private async Task<int> CalculateReviewStreakAsync(
            int userId, ReviewPeriodType periodType, DateTimeOffset currentPeriodStart,
            CancellationToken cancellationToken)
        {
            List<TradingReview> completedReviews = await context.TradingReviews
                .AsNoTracking()
                .Where(r => r.CreatedBy == userId)
                .Where(r => r.PeriodType == periodType)
                .Where(r => r.Status == ReviewWizardStatus.Completed)
                .OrderByDescending(r => r.PeriodStart)
                .ToListAsync(cancellationToken);

            if (completedReviews.Count == 0) return 0;

            int streak = 0;
            DateTimeOffset expectedPeriodStart = currentPeriodStart;

            foreach (TradingReview review in completedReviews)
            {
                if (review.PeriodStart == expectedPeriodStart)
                {
                    streak++;
                    expectedPeriodStart = GetPreviousPeriodStart(periodType, expectedPeriodStart);
                }
                else if (review.PeriodStart == GetPreviousPeriodStart(periodType, expectedPeriodStart))
                {
                    // Allow skipping current period (it may not be complete yet)
                    expectedPeriodStart = review.PeriodStart;
                    streak++;
                    expectedPeriodStart = GetPreviousPeriodStart(periodType, expectedPeriodStart);
                }
                else
                {
                    break;
                }
            }

            return streak;
        }

        internal static TradingReviewViewModel MapReviewToViewModel(TradingReview review) => new()
        {
            Id = review.Id,
            PeriodType = (int)review.PeriodType,
            PeriodStart = review.PeriodStart,
            PeriodEnd = review.PeriodEnd,
            Status = (int)review.Status,
            CompletedDate = review.CompletedDate,
            ExecutionRating = review.ExecutionRating,
            DisciplineRating = review.DisciplineRating,
            PsychologyRating = review.PsychologyRating,
            RiskManagementRating = review.RiskManagementRating,
            OverallRating = review.OverallRating,
            PerformanceNotes = review.PerformanceNotes,
            BestTradeReflection = review.BestTradeReflection,
            WorstTradeReflection = review.WorstTradeReflection,
            DisciplineNotes = review.DisciplineNotes,
            PsychologyNotes = review.PsychologyNotes,
            GoalsForNextPeriod = review.GoalsForNextPeriod,
            KeyTakeaways = review.KeyTakeaways,
            TotalTrades = review.TotalTrades,
            Wins = review.Wins,
            Losses = review.Losses,
            TotalPnl = review.TotalPnl,
            WinRate = review.WinRate,
            RuleBreaks = review.RuleBreaks,
            ActionItems = [.. review.ActionItems.Select(ai => new ReviewActionItemViewModel
            {
                Id = ai.Id,
                TradingReviewId = ai.TradingReviewId,
                Title = ai.Title,
                Description = ai.Description,
                Priority = (int)ai.Priority,
                Status = (int)ai.Status,
                Category = (int)ai.Category,
                DueDate = ai.DueDate,
                CompletedDate = ai.CompletedDate,
                CompletionNotes = ai.CompletionNotes,
                CreatedDate = ai.CreatedDate,
            })],
        };
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.ReviewWizard);

            group.MapGet("/data", async (
                [FromQuery] ReviewPeriodType periodType,
                [FromQuery] DateTimeOffset periodStart,
                ClaimsPrincipal user,
                ISender sender) =>
            {
                Request request = new()
                {
                    PeriodType = periodType,
                    PeriodStart = periodStart,
                    UserId = user.GetCurrentUserId(),
                };

                Result<ReviewWizardDataViewModel> result = await sender.Send(request);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<ReviewWizardDataViewModel>>(StatusCodes.Status200OK)
            .WithSummary("Get aggregated data for the review wizard.")
            .WithDescription("Returns current + previous period metrics, trade highlights, emotion distribution, discipline summary, pending action items, and review streak.")
            .WithTags(Tags.ReviewWizard)
            .RequireAuthorization();
        }
    }
}
