namespace TradingJournal.Modules.Goals.Features.V1;

using TradingJournal.Shared.Common;

/// <summary>
/// Paginated access to a goal's full progress and activity history. The detail
/// endpoint only previews the most recent entries (see
/// <see cref="GetGoalDetail.HistoryPreviewLimit"/>); this is the "view all".
/// </summary>
public sealed class GetGoalHistory
{
    public sealed record ProgressRequest(int GoalId, int Page = 1, int PageSize = 50, int UserId = 0)
        : IQuery<Result<PaginationViewModel<ProgressEntryView>>>;

    public sealed record ActivityRequest(int GoalId, int Page = 1, int PageSize = 50, int UserId = 0)
        : IQuery<Result<PaginationViewModel<GoalActivityView>>>;

    public sealed class ProgressHandler(IGoalDbContext context)
        : IQueryHandler<ProgressRequest, Result<PaginationViewModel<ProgressEntryView>>>
    {
        public async Task<Result<PaginationViewModel<ProgressEntryView>>> Handle(
            ProgressRequest request,
            CancellationToken cancellationToken)
        {
            (int page, int pageSize) = Normalize(request.Page, request.PageSize);

            if (!await OwnsGoalAsync(context, request.GoalId, request.UserId, cancellationToken))
            {
                return Result<PaginationViewModel<ProgressEntryView>>.Failure(Error.Create("Goal was not found."));
            }

            IQueryable<GoalProgressEntry> query = context.ProgressEntries
                .AsNoTracking()
                .Where(entry => entry.GoalId == request.GoalId);

            int total = await query.CountAsync(cancellationToken);

            List<ProgressEntryView> values = await query
                .OrderByDescending(entry => entry.CreatedDate)
                .ThenByDescending(entry => entry.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(entry => new ProgressEntryView(
                    entry.Id,
                    entry.ItemType,
                    entry.MilestoneId,
                    entry.GoalTaskId,
                    entry.PreviousValue,
                    entry.CurrentValue,
                    entry.PreviousIsCompleted,
                    entry.CurrentIsCompleted,
                    entry.Note,
                    entry.CreatedDate))
                .ToListAsync(cancellationToken);

            return Result<PaginationViewModel<ProgressEntryView>>.Success(new PaginationViewModel<ProgressEntryView>
            {
                TotalItems = total,
                HasMore = page * pageSize < total,
                Values = values,
            });
        }
    }

    public sealed class ActivityHandler(IGoalDbContext context)
        : IQueryHandler<ActivityRequest, Result<PaginationViewModel<GoalActivityView>>>
    {
        public async Task<Result<PaginationViewModel<GoalActivityView>>> Handle(
            ActivityRequest request,
            CancellationToken cancellationToken)
        {
            (int page, int pageSize) = Normalize(request.Page, request.PageSize);

            if (!await OwnsGoalAsync(context, request.GoalId, request.UserId, cancellationToken))
            {
                return Result<PaginationViewModel<GoalActivityView>>.Failure(Error.Create("Goal was not found."));
            }

            IQueryable<GoalActivityLink> query = context.ActivityLinks
                .AsNoTracking()
                .Where(link => link.GoalId == request.GoalId);

            int total = await query.CountAsync(cancellationToken);

            List<GoalActivityView> values = await query
                .OrderByDescending(link => link.RecordedAt)
                .ThenByDescending(link => link.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(link => new GoalActivityView(
                    link.Id,
                    link.ItemType,
                    link.ItemId,
                    link.MetricSource,
                    link.SourceType,
                    link.SourceId,
                    link.Delta,
                    link.CompletedItem,
                    link.RecordedAt))
                .ToListAsync(cancellationToken);

            return Result<PaginationViewModel<GoalActivityView>>.Success(new PaginationViewModel<GoalActivityView>
            {
                TotalItems = total,
                HasMore = page * pageSize < total,
                Values = values,
            });
        }
    }

    private static (int Page, int PageSize) Normalize(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, 200));

    private static Task<bool> OwnsGoalAsync(
        IGoalDbContext context,
        int goalId,
        int userId,
        CancellationToken cancellationToken) =>
        context.Goals
            .AsNoTracking()
            .AnyAsync(goal => goal.Id == goalId && goal.CreatedBy == userId, cancellationToken);

    [ExcludeFromCodeCoverage]
    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet($"{ApiGroup.V1.Goals}/{{goalId:int}}/history/progress", async (
                int goalId,
                [FromQuery] int page,
                [FromQuery] int pageSize,
                ClaimsPrincipal user,
                ISender sender) =>
            {
                Result<PaginationViewModel<ProgressEntryView>> result = await sender.Send(
                    new ProgressRequest(goalId, page <= 0 ? 1 : page, pageSize <= 0 ? 50 : pageSize, user.GetCurrentUserId()));
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .Produces<Result<PaginationViewModel<ProgressEntryView>>>()
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Paginated progress history for a goal.")
            .WithTags(Tags.Goals)
            .RequireAuthorization();

            app.MapGet($"{ApiGroup.V1.Goals}/{{goalId:int}}/history/activity", async (
                int goalId,
                [FromQuery] int page,
                [FromQuery] int pageSize,
                ClaimsPrincipal user,
                ISender sender) =>
            {
                Result<PaginationViewModel<GoalActivityView>> result = await sender.Send(
                    new ActivityRequest(goalId, page <= 0 ? 1 : page, pageSize <= 0 ? 50 : pageSize, user.GetCurrentUserId()));
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .Produces<Result<PaginationViewModel<GoalActivityView>>>()
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Paginated auto-tracking activity history for a goal.")
            .WithTags(Tags.Goals)
            .RequireAuthorization();
        }
    }
}
