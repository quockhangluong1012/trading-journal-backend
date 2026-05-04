using TradingJournal.Shared.Common;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Trades.Features.V1.ReviewWizard;

public sealed class SaveReviewWizard
{
    public sealed record Request(
        ReviewPeriodType PeriodType,
        DateTimeOffset PeriodStart,
        DateTimeOffset PeriodEnd,
        bool MarkAsCompleted,
        int? ExecutionRating,
        int? DisciplineRating,
        int? PsychologyRating,
        int? RiskManagementRating,
        int? OverallRating,
        string? PerformanceNotes,
        string? BestTradeReflection,
        string? WorstTradeReflection,
        string? DisciplineNotes,
        string? PsychologyNotes,
        string? GoalsForNextPeriod,
        string? KeyTakeaways,
        int TotalTrades, int Wins, int Losses,
        decimal TotalPnl, decimal WinRate, int RuleBreaks,
        List<ActionItemRequest>? ActionItems) : ICommand<Result<TradingReviewViewModel>>, IUserAwareRequest
    {
        public int UserId { get; set; }
    }

    public sealed record ActionItemRequest(
        int? Id, string Title, string? Description,
        ActionItemPriority Priority, ActionItemStatus Status,
        LessonCategory Category, DateTimeOffset? DueDate);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.PeriodType).Must(Enum.IsDefined);
            RuleFor(x => x.ExecutionRating).InclusiveBetween(1, 5).When(x => x.ExecutionRating.HasValue);
            RuleFor(x => x.DisciplineRating).InclusiveBetween(1, 5).When(x => x.DisciplineRating.HasValue);
            RuleFor(x => x.PsychologyRating).InclusiveBetween(1, 5).When(x => x.PsychologyRating.HasValue);
            RuleFor(x => x.RiskManagementRating).InclusiveBetween(1, 5).When(x => x.RiskManagementRating.HasValue);
            RuleFor(x => x.OverallRating).InclusiveBetween(1, 5).When(x => x.OverallRating.HasValue);
            RuleFor(x => x.PerformanceNotes).MaximumLength(2000);
            RuleFor(x => x.BestTradeReflection).MaximumLength(2000);
            RuleFor(x => x.WorstTradeReflection).MaximumLength(2000);
            RuleFor(x => x.DisciplineNotes).MaximumLength(2000);
            RuleFor(x => x.PsychologyNotes).MaximumLength(2000);
            RuleFor(x => x.GoalsForNextPeriod).MaximumLength(2000);
            RuleFor(x => x.KeyTakeaways).MaximumLength(2000);
            RuleForEach(x => x.ActionItems).ChildRules(ai =>
            {
                ai.RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
                ai.RuleFor(x => x.Description).MaximumLength(1000);
            });
        }
    }

    public sealed class Handler(ITradeDbContext context)
        : ICommandHandler<Request, Result<TradingReviewViewModel>>
    {
        public async Task<Result<TradingReviewViewModel>> Handle(
            Request request, CancellationToken cancellationToken)
        {
            ReviewPeriodBounds bounds = ReviewPeriodCalculator.GetBounds(request.PeriodType, request.PeriodStart);

            TradingReview? review = await context.TradingReviews
                .Include(r => r.ActionItems)
                .Where(r => r.CreatedBy == request.UserId)
                .Where(r => r.PeriodType == request.PeriodType)
                .Where(r => r.PeriodStart == bounds.Start)
                .FirstOrDefaultAsync(cancellationToken);

            bool isNew = review is null;
            if (isNew)
            {
                review = new TradingReview
                {
                    Id = 0,
                    PeriodType = request.PeriodType,
                    PeriodStart = bounds.Start,
                    PeriodEnd = bounds.End,
                };
            }

            review!.ExecutionRating = request.ExecutionRating;
            review.DisciplineRating = request.DisciplineRating;
            review.PsychologyRating = request.PsychologyRating;
            review.RiskManagementRating = request.RiskManagementRating;
            review.OverallRating = request.OverallRating;
            review.PerformanceNotes = request.PerformanceNotes;
            review.BestTradeReflection = request.BestTradeReflection;
            review.WorstTradeReflection = request.WorstTradeReflection;
            review.DisciplineNotes = request.DisciplineNotes;
            review.PsychologyNotes = request.PsychologyNotes;
            review.GoalsForNextPeriod = request.GoalsForNextPeriod;
            review.KeyTakeaways = request.KeyTakeaways;
            review.TotalTrades = request.TotalTrades;
            review.Wins = request.Wins;
            review.Losses = request.Losses;
            review.TotalPnl = request.TotalPnl;
            review.WinRate = request.WinRate;
            review.RuleBreaks = request.RuleBreaks;

            if (request.MarkAsCompleted && review.Status != ReviewWizardStatus.Completed)
            {
                review.Status = ReviewWizardStatus.Completed;
                review.CompletedDate = DateTimeOffset.UtcNow;
            }

            SyncActionItems(review, request.ActionItems);

            if (isNew) await context.TradingReviews.AddAsync(review, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return Result<TradingReviewViewModel>.Success(
                GetWizardData.Handler.MapReviewToViewModel(review));
        }

        private static void SyncActionItems(TradingReview review, List<ActionItemRequest>? items)
        {
            if (items is null) return;

            HashSet<int> keepIds = [.. items.Where(ai => ai.Id.HasValue).Select(ai => ai.Id!.Value)];
            List<ReviewActionItem> toRemove = [.. review.ActionItems.Where(e => !keepIds.Contains(e.Id))];
            foreach (var item in toRemove) review.ActionItems.Remove(item);

            foreach (var req in items)
            {
                if (req.Id.HasValue)
                {
                    var existing = review.ActionItems.FirstOrDefault(ai => ai.Id == req.Id.Value);
                    if (existing is not null)
                    {
                        existing.Title = req.Title;
                        existing.Description = req.Description;
                        existing.Priority = req.Priority;
                        existing.Status = req.Status;
                        existing.Category = req.Category;
                        existing.DueDate = req.DueDate;
                        if (req.Status == ActionItemStatus.Completed && !existing.CompletedDate.HasValue)
                            existing.CompletedDate = DateTimeOffset.UtcNow;
                    }
                }
                else
                {
                    review.ActionItems.Add(new ReviewActionItem
                    {
                        Id = 0, Title = req.Title, Description = req.Description,
                        Priority = req.Priority, Status = req.Status,
                        Category = req.Category, DueDate = req.DueDate,
                    });
                }
            }
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGroup(ApiGroup.V1.ReviewWizard)
                .MapPost("/", async ([FromBody] Request request, ISender sender) =>
                {
                    var result = await sender.Send(request);
                    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
                })
                .Produces<Result<TradingReviewViewModel>>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .WithSummary("Save or complete a review wizard.")
                .WithTags(Tags.ReviewWizard)
                .RequireAuthorization();
        }
    }
}
