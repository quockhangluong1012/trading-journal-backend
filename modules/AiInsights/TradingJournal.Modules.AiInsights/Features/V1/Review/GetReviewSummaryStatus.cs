using TradingJournal.Shared.Common;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.AiInsights.Features.V1.Review;

public sealed class GetReviewSummaryStatus
{
    public sealed record Request(ReviewPeriodType PeriodType, DateTimeOffset PeriodStart, int UserId = 0)
        : IQuery<Result<ReviewSummaryStatusViewModel>>;

    public sealed record ReviewSummaryStatusViewModel(
        bool IsGenerating, string? AiSummary, string? AiStrengths, string? AiWeaknesses,
        string? AiActionItems, string? AiTechnicalInsights, string? AiPsychologyAnalysis,
        string? AiCriticalMistakesTechnical, string? AiCriticalMistakesPsychological, string? AiWhatToImprove);

    public sealed class Handler(IAiInsightsDbContext context)
        : IQueryHandler<Request, Result<ReviewSummaryStatusViewModel>>
    {
        public async Task<Result<ReviewSummaryStatusViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            ReviewPeriodBounds period = ReviewPeriodCalculator.GetBounds(request.PeriodType, request.PeriodStart);

            TradingReview? review = await context.TradingReviews
                .AsNoTracking()
                .FirstOrDefaultAsync(r =>
                    r.CreatedBy == request.UserId &&
                    r.PeriodType == request.PeriodType &&
                    r.PeriodStart == period.Start,
                    cancellationToken);

            if (review is null)
            {
                return Result<ReviewSummaryStatusViewModel>.Success(
                    new ReviewSummaryStatusViewModel(false, null, null, null, null, null, null, null, null, null));
            }

            return Result<ReviewSummaryStatusViewModel>.Success(
                new ReviewSummaryStatusViewModel(
                    review.AiSummaryGenerating, review.AiSummary, review.AiStrengths, review.AiWeaknesses,
                    review.AiActionItems, review.AiTechnicalInsights, review.AiPsychologyAnalysis,
                    review.AiCriticalMistakesTechnical, review.AiCriticalMistakesPsychological, review.AiWhatToImprove));
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Reviews);
            group.MapGet("/summary-status", async (ReviewPeriodType periodType, DateTimeOffset periodStart, ClaimsPrincipal user, ISender sender) =>
            {
                Result<ReviewSummaryStatusViewModel> result = await sender.Send(new Request(periodType, periodStart) with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result<ReviewSummaryStatusViewModel>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get review summary generation status.")
            .WithDescription("Returns the current generation status and summary data for polling.")
            .WithTags(Tags.Reviews).RequireAuthorization();
        }
    }
}
