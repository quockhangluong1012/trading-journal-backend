using TradingJournal.Shared.Dtos;

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
        double TotalPnl,
        double WinRate,
        int TotalTrades,
        int Wins,
        int Losses);

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

    public sealed class Handler(ITradeDbContext context, ITradeProvider tradeProvider) : IQueryHandler<Request, Result<ReviewViewModel>>
    {
        public async Task<Result<ReviewViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            // Try to find existing saved review
            TradingReview? existingReview = await context.TradingReviews
                .AsNoTracking()
                .FirstOrDefaultAsync(r =>
                    r.CreatedBy == request.UserId &&
                    r.PeriodType == request.PeriodType &&
                    r.PeriodStart == request.PeriodStart,
                    cancellationToken);

            if (existingReview is not null)
            {
                return Result<ReviewViewModel>.Success(new ReviewViewModel(
                    existingReview.Id,
                    existingReview.PeriodType,
                    existingReview.PeriodStart,
                    existingReview.PeriodEnd,
                    existingReview.UserNotes,
                    existingReview.AiSummary,
                    existingReview.AiStrengths,
                    existingReview.AiWeaknesses,
                    existingReview.AiActionItems,
                    existingReview.AiTechnicalInsights,
                    existingReview.AiPsychologyAnalysis,
                    existingReview.AiCriticalMistakesTechnical,
                    existingReview.AiCriticalMistakesPsychological,
                    existingReview.AiWhatToImprove,
                    existingReview.AiSummaryGenerating,
                    existingReview.TotalPnl,
                    existingReview.WinRate,
                    existingReview.TotalTrades,
                    existingReview.Wins,
                    existingReview.Losses));
            }

            // No saved review — compute metrics from cached trades
            DateTime periodEnd = GetPeriodEnd(request.PeriodType, request.PeriodStart);

            List<TradeCacheDto> allTrades = await tradeProvider.GetTradesAsync(cancellationToken);
            List<TradeCacheDto> periodTrades = [.. allTrades
                .Where(t => t.CreatedBy == request.UserId)
                .Where(t => t.Status == TradeStatus.Closed && t.Pnl.HasValue)
                .Where(t => t.ClosedDate.HasValue && t.ClosedDate.Value >= request.PeriodStart && t.ClosedDate.Value <= periodEnd)];

            int wins = periodTrades.Count(t => t.Pnl > 0);
            int losses = periodTrades.Count(t => t.Pnl <= 0);
            double totalPnl = periodTrades.Sum(t => (double)t.Pnl!.Value);
            double winRate = periodTrades.Count > 0 ? (double)wins / periodTrades.Count * 100 : 0;

            TradingReview newReview = new()
            {
                Id = 0,
                PeriodType = request.PeriodType,
                PeriodStart = request.PeriodStart,
                PeriodEnd = periodEnd,
                TotalPnl = Math.Round(totalPnl, 2),
                WinRate = Math.Round(winRate, 1),
                TotalTrades = periodTrades.Count,
                Wins = wins,
                Losses = losses
            };

            await context.TradingReviews.AddAsync(newReview, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return Result<ReviewViewModel>.Success(new ReviewViewModel(
                newReview.Id,
                request.PeriodType,
                request.PeriodStart,
                periodEnd,
                null, null, null, null, null,
                null, null, null, null, null,
                false,
                newReview.TotalPnl,
                newReview.WinRate,
                newReview.TotalTrades,
                newReview.Wins,
                newReview.Losses));
        }

        private static DateTime GetPeriodEnd(ReviewPeriodType periodType, DateTime periodStart) => periodType switch
        {
            ReviewPeriodType.Daily => periodStart.Date.AddDays(1).AddTicks(-1),
            ReviewPeriodType.Weekly => periodStart.Date.AddDays(7).AddTicks(-1),
            ReviewPeriodType.Monthly => periodStart.Date.AddMonths(1).AddTicks(-1),
            ReviewPeriodType.Quarterly => periodStart.Date.AddMonths(3).AddTicks(-1),
            _ => periodStart.Date.AddDays(1).AddTicks(-1)
        };
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Reviews);

            group.MapGet("/", async (ReviewPeriodType periodType, DateTime periodStart, ISender sender) =>
            {
                Result<ReviewViewModel> result = await sender.Send(new Request(periodType, periodStart));

                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result<ReviewViewModel>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get review for a period.")
            .WithDescription("Retrieves a review for the specified period type and start date.")
            .WithTags(Tags.Reviews)
            .RequireAuthorization();
        }
    }
}
