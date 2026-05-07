using TradingJournal.Shared.Common;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.AiInsights.Features.V1.Review;

public sealed class SaveReview
{
    public sealed record Request(
        ReviewPeriodType PeriodType, DateTime PeriodStart, DateTime PeriodEnd,
        string? UserNotes, int UserId = 0) : ICommand<Result<int>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.PeriodType).IsInEnum()
                .WithErrorCode(HttpStatusCode.BadRequest.ToString()).WithMessage("Invalid period type.");
            RuleFor(x => x.PeriodStart).LessThanOrEqualTo(x => x.PeriodEnd)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString()).WithMessage("Period start must be before period end.");
        }
    }

    public sealed class Handler(IAiInsightsDbContext context, IAiTradeDataProvider tradeDataProvider)
        : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            ReviewSnapshot snapshot = await tradeDataProvider.BuildReviewSnapshotAsync(
                request.PeriodType, request.PeriodStart, request.UserId, cancellationToken);
            ReviewSnapshotMetrics metrics = snapshot.Metrics;

            TradingReview? existing = await context.TradingReviews
                .FirstOrDefaultAsync(r =>
                    r.CreatedBy == request.UserId &&
                    r.PeriodType == request.PeriodType &&
                    r.PeriodStart == snapshot.PeriodStart,
                    cancellationToken);

            if (existing is not null)
            {
                existing.PeriodEnd = snapshot.PeriodEnd;
                existing.UserNotes = request.UserNotes;
                existing.TotalPnl = metrics.TotalPnl;
                existing.WinRate = metrics.WinRate;
                existing.TotalTrades = metrics.TotalTrades;
                existing.Wins = metrics.Wins;
                existing.Losses = metrics.Losses;
                existing.RuleBreaks = metrics.RuleBreakTrades;
                context.TradingReviews.Update(existing);
                await context.SaveChangesAsync(cancellationToken);
                return Result<int>.Success(existing.Id);
            }

            TradingReview review = new()
            {
                Id = 0, PeriodType = request.PeriodType,
                PeriodStart = snapshot.PeriodStart, PeriodEnd = snapshot.PeriodEnd,
                UserNotes = request.UserNotes,
                TotalPnl = metrics.TotalPnl, WinRate = metrics.WinRate,
                TotalTrades = metrics.TotalTrades, Wins = metrics.Wins, Losses = metrics.Losses,
                RuleBreaks = metrics.RuleBreakTrades,
            };
            await context.TradingReviews.AddAsync(review, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            return Result<int>.Success(review.Id);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Reviews);
            group.MapPost("/", async (ISender sender, [FromBody] Request request, ClaimsPrincipal user) =>
            {
                Result<int> result = await sender.Send(request with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<int>>().Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Save a review.")
            .WithDescription("Creates or updates a review for the specified period.")
            .WithTags(Tags.Reviews).RequireAuthorization();
        }
    }
}
