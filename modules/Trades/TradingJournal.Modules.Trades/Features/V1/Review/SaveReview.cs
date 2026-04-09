using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Trades.Features.V1.Review;

public sealed class SaveReview
{
    public sealed record Request(
        ReviewPeriodType PeriodType,
        DateTime PeriodStart,
        DateTime PeriodEnd,
        string? UserNotes,
        int UserId = 0) : ICommand<Result<int>>;

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

    public sealed class Handler(ITradeDbContext context, ITradeProvider tradeProvider)
        : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            // Compute fresh metrics
            List<TradeCacheDto> allTrades = await tradeProvider.GetTradesAsync(cancellationToken);
            List<TradeCacheDto> periodTrades = [.. allTrades
                .Where(t => t.CreatedBy == request.UserId)
                .Where(t => t.Status == TradeStatus.Closed && t.Pnl.HasValue)
                .Where(t => t.ClosedDate.HasValue && t.ClosedDate.Value >= request.PeriodStart && t.ClosedDate.Value <= request.PeriodEnd)];

            int wins = periodTrades.Count(t => t.Pnl > 0);
            int losses = periodTrades.Count(t => t.Pnl <= 0);
            double totalPnl = periodTrades.Sum(t => (double)t.Pnl!.Value);
            double winRate = periodTrades.Count > 0 ? (double)wins / periodTrades.Count * 100 : 0;

            // Upsert
            TradingReview? existing = await context.TradingReviews
                .FirstOrDefaultAsync(r =>
                    r.CreatedBy == request.UserId &&
                    r.PeriodType == request.PeriodType &&
                    r.PeriodStart == request.PeriodStart,
                    cancellationToken);

            if (existing is not null)
            {
                existing.UserNotes = request.UserNotes;
                existing.TotalPnl = Math.Round(totalPnl, 2);
                existing.WinRate = Math.Round(winRate, 1);
                existing.TotalTrades = periodTrades.Count;
                existing.Wins = wins;
                existing.Losses = losses;

                context.TradingReviews.Update(existing);
                await context.SaveChangesAsync(cancellationToken);
                return Result<int>.Success(existing.Id);
            }

            TradingReview review = new()
            {
                Id = 0,
                PeriodType = request.PeriodType,
                PeriodStart = request.PeriodStart,
                PeriodEnd = request.PeriodEnd,
                UserNotes = request.UserNotes,
                TotalPnl = Math.Round(totalPnl, 2),
                WinRate = Math.Round(winRate, 1),
                TotalTrades = periodTrades.Count,
                Wins = wins,
                Losses = losses,
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

            group.MapPost("/", async (ISender sender, [FromBody] Request request) =>
            {
                Result<int> result = await sender.Send(request);

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<int>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Save a review.")
            .WithDescription("Creates or updates a review for the specified period.")
            .WithTags(Tags.Reviews)
            .RequireAuthorization();
        }
    }
}
