namespace TradingJournal.Modules.Trades.Features.V1.Review;

public sealed class GetReviewSummaryStatus
{
    internal sealed record Request(ReviewPeriodType PeriodType, DateTime PeriodStart, int UserId = 0)
        : IQuery<Result<ReviewSummaryStatusViewModel>>;

    internal sealed record ReviewSummaryStatusViewModel(
        bool IsGenerating,
        string? AiSummary,
        string? AiStrengths,
        string? AiWeaknesses,
        string? AiActionItems,
        string? AiTechnicalInsights,
        string? AiPsychologyAnalysis,
        string? AiCriticalMistakesTechnical,
        string? AiCriticalMistakesPsychological,
        string? AiWhatToImprove);

    internal sealed class Handler(ITradeDbContext context)
        : IQueryHandler<Request, Result<ReviewSummaryStatusViewModel>>
    {
        public async Task<Result<ReviewSummaryStatusViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            TradingReview? review = await context.TradingReviews
                .AsNoTracking()
                .FirstOrDefaultAsync(r =>
                    r.CreatedBy == request.UserId &&
                    r.PeriodType == request.PeriodType &&
                    r.PeriodStart == request.PeriodStart,
                    cancellationToken);

            if (review is null)
            {
                return Result<ReviewSummaryStatusViewModel>.Success(
                    new ReviewSummaryStatusViewModel(false, null, null, null, null, null, null, null, null, null));
            }

            return Result<ReviewSummaryStatusViewModel>.Success(
                new ReviewSummaryStatusViewModel(
                    review.AiSummaryGenerating,
                    review.AiSummary,
                    review.AiStrengths,
                    review.AiWeaknesses,
                    review.AiActionItems,
                    review.AiTechnicalInsights,
                    review.AiPsychologyAnalysis,
                    review.AiCriticalMistakesTechnical,
                    review.AiCriticalMistakesPsychological,
                    review.AiWhatToImprove));
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Reviews);

            group.MapGet("/summary-status", async (ReviewPeriodType periodType, DateTime periodStart, ISender sender) =>
            {
                Result<ReviewSummaryStatusViewModel> result = await sender.Send(new Request(periodType, periodStart));

                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result<ReviewSummaryStatusViewModel>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get review summary generation status.")
            .WithDescription("Returns the current generation status and summary data for polling.")
            .WithTags(Tags.Reviews)
            .RequireAuthorization();
        }
    }
}
