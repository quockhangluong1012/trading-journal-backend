using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Modules.Trades.Events;

namespace TradingJournal.Modules.Trades.Features.V1.Review;

public sealed class GenerateReviewSummary
{
    public sealed record Request(
        ReviewPeriodType PeriodType,
        DateTime PeriodStart,
        DateTime PeriodEnd,
        int UserId = 0) : ICommand<Result<bool>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.PeriodType)
                .IsInEnum()
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Invalid period type.");

            RuleFor(x => x.PeriodStart)
                .LessThanOrEqualTo(x => x.PeriodEnd)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Period start must be before period end.");
        }
    }

    public sealed class Handler(ITradeDbContext context, IEventBus eventBus)
        : ICommandHandler<Request, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Request request, CancellationToken cancellationToken)
        {
            // Set generating flag on existing review (or create one)
            TradingReview? existing = await context.TradingReviews
                .FirstOrDefaultAsync(r =>
                    r.CreatedBy == request.UserId &&
                    r.PeriodType == request.PeriodType &&
                    r.PeriodStart == request.PeriodStart,
                    cancellationToken);

            if (existing is not null)
            {
                existing.AiSummaryGenerating = true;
                context.TradingReviews.Update(existing);
            }
            else
            {
                TradingReview review = new()
                {
                    Id = 0,
                    PeriodType = request.PeriodType,
                    PeriodStart = request.PeriodStart,
                    PeriodEnd = request.PeriodEnd,
                    AiSummaryGenerating = true,
                };

                await context.TradingReviews.AddAsync(review, cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);

            // Publish event for background processing
            await eventBus.PublishAsync(
                new GenerateReviewSummaryEvent(
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    request.PeriodType,
                    request.PeriodStart,
                    request.PeriodEnd,
                    request.UserId),
                cancellationToken);

            return Result<bool>.Success(true);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Reviews);

            group.MapPost("/generate-summary", async (ISender sender, [FromBody] Request request) =>
            {
                Result<bool> result = await sender.Send(request);

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<bool>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Generate AI review summary.")
            .WithDescription("Publishes an event to generate a review summary asynchronously. Poll the status endpoint for results.")
            .WithTags(Tags.Reviews)
            .RequireAuthorization();
        }
    }
}
