namespace TradingJournal.Modules.Trades.Features.V1.Review;

public sealed class GetReview
{
    public sealed record Request(ReviewPeriodType PeriodType, DateTime PeriodStart, int UserId = 0)
        : IQuery<Result<ReviewViewModel>>;

    public sealed record ReviewViewModel(
        int? Id,
        ReviewPeriodType PeriodType,
        DateTime PeriodStart,
        DateTime PeriodEnd,
        string? UserNotes,
        string? AiSummary,
        string? AiStrengths,
        string? AiWeaknesses,
        string? AiActionItems,
        string? AiTechnicalInsights,
        string? AiPsychologyAnalysis,
        string? AiCriticalMistakesTechnical,
        string? AiCriticalMistakesPsychological,
        string? AiWhatToImprove,
        bool AiSummaryGenerating,
        decimal TotalPnl,
        decimal WinRate,
        int TotalTrades,
        int Wins,
        int Losses,
        decimal AverageWin,
        decimal AverageLoss,
        decimal BestTradePnl,
        decimal WorstTradePnl,
        decimal BestDayPnl,
        decimal WorstDayPnl,
        int LongTrades,
        int ShortTrades,
        int RuleBreakTrades,
        int HighConfidenceTrades,
        string? TopAsset,
        string? PrimaryTradingZone,
        string? DominantEmotion,
        string? TopTechnicalTheme);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.PeriodType)
                .IsInEnum()
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Invalid period type.");
        }
    }

    public sealed class Handler(ITradeDbContext context, IReviewSnapshotBuilder reviewSnapshotBuilder) : IQueryHandler<Request, Result<ReviewViewModel>>
    {
        public async Task<Result<ReviewViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            ReviewSnapshot snapshot = await reviewSnapshotBuilder.BuildAsync(
                request.PeriodType,
                request.PeriodStart,
                request.UserId,
                cancellationToken);

            TradingReview? existingReview = await context.TradingReviews
                .AsNoTracking()
                .FirstOrDefaultAsync(r =>
                    r.CreatedBy == request.UserId &&
                    r.PeriodType == request.PeriodType &&
                    r.PeriodStart == snapshot.PeriodStart,
                    cancellationToken);

            return Result<ReviewViewModel>.Success(ToViewModel(existingReview, snapshot));
        }

        private static ReviewViewModel ToViewModel(TradingReview? review, ReviewSnapshot snapshot)
        {
            ReviewSnapshotMetrics metrics = snapshot.Metrics;

            return new ReviewViewModel(
                review?.Id,
                snapshot.PeriodType,
                snapshot.PeriodStart,
                snapshot.PeriodEnd,
                review?.UserNotes,
                review?.AiSummary,
                review?.AiStrengths,
                review?.AiWeaknesses,
                review?.AiActionItems,
                review?.AiTechnicalInsights,
                review?.AiPsychologyAnalysis,
                review?.AiCriticalMistakesTechnical,
                review?.AiCriticalMistakesPsychological,
                review?.AiWhatToImprove,
                review?.AiSummaryGenerating ?? false,
                metrics.TotalPnl,
                metrics.WinRate,
                metrics.TotalTrades,
                metrics.Wins,
                metrics.Losses,
                metrics.AverageWin,
                metrics.AverageLoss,
                metrics.BestTradePnl,
                metrics.WorstTradePnl,
                metrics.BestDayPnl,
                metrics.WorstDayPnl,
                metrics.LongTrades,
                metrics.ShortTrades,
                metrics.RuleBreakTrades,
                metrics.HighConfidenceTrades,
                metrics.TopAsset,
                metrics.PrimaryTradingZone,
                metrics.DominantEmotion,
                metrics.TopTechnicalTheme);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Reviews);

            group.MapGet("/", async (ReviewPeriodType periodType, DateTime periodStart, ClaimsPrincipal user, ISender sender) =>
            {
                Result<ReviewViewModel> result = await sender.Send(new Request(periodType, periodStart) with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result<ReviewViewModel>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get review for a period.")
            .WithDescription("Retrieves a review for the specified period type and start date.")
            .WithTags(Tags.Reviews)
            .RequireAuthorization();
        }
    }
}
