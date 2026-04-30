namespace TradingJournal.Modules.Trades.Features.V1.ReviewWizard;

public sealed class ManageActionItems
{
    #region Update Action Item

    public sealed record UpdateRequest(
        int Id, ActionItemStatus Status, string? CompletionNotes
    ) : ICommand<Result<ReviewActionItemViewModel>>, IUserAwareRequest
    {
        public int UserId { get; set; }
    }

    public sealed class UpdateHandler(ITradeDbContext context)
        : ICommandHandler<UpdateRequest, Result<ReviewActionItemViewModel>>
    {
        public async Task<Result<ReviewActionItemViewModel>> Handle(
            UpdateRequest request, CancellationToken cancellationToken)
        {
            ReviewActionItem? item = await context.ReviewActionItems
                .Where(ai => ai.Id == request.Id && ai.CreatedBy == request.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (item is null)
                return Result<ReviewActionItemViewModel>.Failure(
                    Error.Create("Action item not found."));

            item.Status = request.Status;
            if (!string.IsNullOrWhiteSpace(request.CompletionNotes))
                item.CompletionNotes = request.CompletionNotes;

            if (request.Status == ActionItemStatus.Completed && !item.CompletedDate.HasValue)
                item.CompletedDate = DateTime.UtcNow;

            await context.SaveChangesAsync(cancellationToken);

            return Result<ReviewActionItemViewModel>.Success(new ReviewActionItemViewModel
            {
                Id = item.Id,
                TradingReviewId = item.TradingReviewId,
                Title = item.Title,
                Description = item.Description,
                Priority = (int)item.Priority,
                Status = (int)item.Status,
                Category = (int)item.Category,
                DueDate = item.DueDate,
                CompletedDate = item.CompletedDate,
                CompletionNotes = item.CompletionNotes,
                CreatedDate = item.CreatedDate,
            });
        }
    }

    #endregion

    #region Get All Action Items

    public sealed class ListRequest : IQuery<Result<List<ReviewActionItemViewModel>>>, IUserAwareRequest
    {
        public ActionItemStatus? StatusFilter { get; set; }
        public int UserId { get; set; }
    }

    public sealed class ListHandler(ITradeDbContext context)
        : IQueryHandler<ListRequest, Result<List<ReviewActionItemViewModel>>>
    {
        public async Task<Result<List<ReviewActionItemViewModel>>> Handle(
            ListRequest request, CancellationToken cancellationToken)
        {
            IQueryable<ReviewActionItem> query = context.ReviewActionItems
                .AsNoTracking()
                .Where(ai => ai.CreatedBy == request.UserId);

            if (request.StatusFilter.HasValue)
                query = query.Where(ai => ai.Status == request.StatusFilter.Value);

            List<ReviewActionItemViewModel> items = await query
                .OrderByDescending(ai => ai.Priority)
                .ThenByDescending(ai => ai.CreatedDate)
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

            return Result<List<ReviewActionItemViewModel>>.Success(items);
        }
    }

    #endregion

    #region Get Review Streak

    public sealed class StreakRequest : IQuery<Result<ReviewStreakViewModel>>, IUserAwareRequest
    {
        public ReviewPeriodType PeriodType { get; set; }
        public int UserId { get; set; }
    }

    public sealed class StreakHandler(ITradeDbContext context)
        : IQueryHandler<StreakRequest, Result<ReviewStreakViewModel>>
    {
        public async Task<Result<ReviewStreakViewModel>> Handle(
            StreakRequest request, CancellationToken cancellationToken)
        {
            List<TradingReview> reviews = await context.TradingReviews
                .AsNoTracking()
                .Where(r => r.CreatedBy == request.UserId && r.PeriodType == request.PeriodType)
                .Where(r => r.Status == ReviewWizardStatus.Completed)
                .OrderByDescending(r => r.PeriodStart)
                .ToListAsync(cancellationToken);

            int currentStreak = 0, longestStreak = 0, tempStreak = 0;
            DateTime? lastDate = null;

            foreach (var review in reviews)
            {
                if (lastDate is null)
                {
                    tempStreak = 1;
                }
                else
                {
                    DateTime expected = GetPrev(request.PeriodType, lastDate.Value);
                    tempStreak = review.PeriodStart == expected ? tempStreak + 1 : 1;
                }

                longestStreak = Math.Max(longestStreak, tempStreak);
                if (lastDate is null || review.PeriodStart == GetPrev(request.PeriodType, lastDate.Value))
                    currentStreak = tempStreak;

                lastDate = review.PeriodStart;
            }

            return Result<ReviewStreakViewModel>.Success(new ReviewStreakViewModel
            {
                CurrentStreak = currentStreak,
                LongestStreak = longestStreak,
                TotalReviews = reviews.Count,
                LastReviewDate = reviews.FirstOrDefault()?.CompletedDate,
            });
        }

        private static DateTime GetPrev(ReviewPeriodType pt, DateTime d) => pt switch
        {
            ReviewPeriodType.Daily => d.AddDays(-1),
            ReviewPeriodType.Weekly => d.AddDays(-7),
            ReviewPeriodType.Monthly => d.AddMonths(-1),
            ReviewPeriodType.Quarterly => d.AddMonths(-3),
            _ => d.AddDays(-7),
        };
    }

    #endregion

    public sealed class Endpoints : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.ActionItems);

            group.MapGet("/", async (
                [FromQuery] int? status,
                ClaimsPrincipal user, ISender sender) =>
            {
                var request = new ListRequest
                {
                    StatusFilter = status.HasValue ? (ActionItemStatus)status.Value : null,
                    UserId = user.GetCurrentUserId(),
                };
                var result = await sender.Send(request);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<List<ReviewActionItemViewModel>>>(StatusCodes.Status200OK)
            .WithSummary("List all action items.")
            .WithTags(Tags.ActionItems)
            .RequireAuthorization();

            group.MapPut("/{id:int}/status", async (
                int id, [FromBody] UpdateRequest request, ISender sender) =>
            {
                var cmd = request with { Id = id };
                var result = await sender.Send(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .Produces<Result<ReviewActionItemViewModel>>(StatusCodes.Status200OK)
            .WithSummary("Update action item status.")
            .WithTags(Tags.ActionItems)
            .RequireAuthorization();

            // Streak endpoint
            RouteGroupBuilder wizardGroup = app.MapGroup(ApiGroup.V1.ReviewWizard);

            wizardGroup.MapGet("/streak", async (
                [FromQuery] ReviewPeriodType periodType,
                ClaimsPrincipal user, ISender sender) =>
            {
                var request = new StreakRequest
                {
                    PeriodType = periodType,
                    UserId = user.GetCurrentUserId(),
                };
                var result = await sender.Send(request);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<ReviewStreakViewModel>>(StatusCodes.Status200OK)
            .WithSummary("Get review completion streak.")
            .WithTags(Tags.ReviewWizard)
            .RequireAuthorization();
        }
    }
}
