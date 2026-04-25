using TradingJournal.Shared.Common;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Trades.Features.V1.Review;

public sealed class GetReviewTrades
{
    public sealed record Request(
        DateTime FromDate,
        DateTime ToDate,
        int Page = 1,
        int PageSize = 50,
        int UserId = 0) : IQuery<Result<PaginationViewModel<ReviewTradeViewModel>>>;

    public sealed record ReviewTradeViewModel(
        int Id,
        string Asset,
        string Position,
        decimal? Pnl,
        DateTime Date,
        DateTime? ClosedDate,
        decimal EntryPrice,
        decimal? ExitPrice,
        int ConfidenceLevel,
        string? TradingZone,
        bool IsRuleBroken,
        string? RuleBreakReason,
        string? Notes,
        IReadOnlyList<string> EmotionTags,
        IReadOnlyList<string> TechnicalThemes,
        IReadOnlyList<string> ChecklistItems);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.FromDate)
                .LessThanOrEqualTo(x => x.ToDate)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("FromDate must be before ToDate.");

            RuleFor(x => x.Page)
                .GreaterThan(0)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Page must be greater than 0.");
        }
    }

    public sealed class Handler(ITradeDbContext context, IEmotionTagProvider emotionTagProvider) : IQueryHandler<Request, Result<PaginationViewModel<ReviewTradeViewModel>>>
    {
        public async Task<Result<PaginationViewModel<ReviewTradeViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            DateTime fromDate = request.FromDate.Date;
            DateTime toDate = request.ToDate.Date.AddDays(1).AddTicks(-1);

            IQueryable<TradeHistory> baseQuery = context.TradeHistories
                .AsNoTracking()
                .Where(th => th.CreatedBy == request.UserId)
                .Where(th => th.Status == TradeStatus.Closed && th.Pnl.HasValue)
                .Where(th => th.ClosedDate.HasValue && th.ClosedDate.Value >= fromDate && th.ClosedDate.Value <= toDate);

            int totalItems = await baseQuery.CountAsync(cancellationToken);

            List<TradeHistory> trades = await baseQuery
                .OrderByDescending(th => th.ClosedDate)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Include(th => th.TradeEmotionTags)
                .Include(th => th.TradeTechnicalAnalysisTags)
                    .ThenInclude(tag => tag.TechnicalAnalysis)
                .Include(th => th.TradeChecklists)
                    .ThenInclude(tc => tc.PretradeChecklist)
                .Include(th => th.TradingZone)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            // Resolve emotion tag names via shared provider
            Dictionary<int, string> emotionNamesById = await LoadEmotionNamesAsync(cancellationToken);

            List<ReviewTradeViewModel> viewModels = [.. trades.Select(th => MapToViewModel(th, emotionNamesById))];

            PaginationViewModel<ReviewTradeViewModel> result = new()
            {
                TotalItems = totalItems,
                HasMore = (request.Page * request.PageSize) < totalItems,
                Values = viewModels
            };

            return Result<PaginationViewModel<ReviewTradeViewModel>>.Success(result);
        }

        private static ReviewTradeViewModel MapToViewModel(TradeHistory th, IReadOnlyDictionary<int, string> emotionNamesById)
        {
            IReadOnlyList<string> emotionTags = [.. th.TradeEmotionTags?
                .Select(tag => emotionNamesById.TryGetValue(tag.EmotionTagId, out string? name) ? name : string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name)) ?? []];

            IReadOnlyList<string> technicalThemes = [.. th.TradeTechnicalAnalysisTags
                .Select(tag => tag.TechnicalAnalysis?.Name ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))];

            IReadOnlyList<string> checklistItems = [.. th.TradeChecklists
                .Select(item => item.PretradeChecklist.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))];

            return new ReviewTradeViewModel(
                th.Id,
                th.Asset,
                th.Position.ToString(),
                th.Pnl,
                th.Date,
                th.ClosedDate,
                th.EntryPrice,
                th.ExitPrice,
                (int)th.ConfidenceLevel,
                th.TradingZoneId.HasValue ? th.TradingZone.Name : null,
                th.IsRuleBroken,
                th.RuleBreakReason,
                th.Notes,
                emotionTags,
                technicalThemes,
                checklistItems);
        }

        private async Task<Dictionary<int, string>> LoadEmotionNamesAsync(CancellationToken cancellationToken)
        {
            List<EmotionTagCacheDto> emotionTags = await emotionTagProvider.GetEmotionTagsAsync(cancellationToken);

            return emotionTags
                .GroupBy(tag => tag.Id)
                .ToDictionary(group => group.Key, group => group.First().Name);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Reviews);

            group.MapPost("/trades", async (ISender sender, [FromBody] Request request, ClaimsPrincipal user) =>
            {
                Result<PaginationViewModel<ReviewTradeViewModel>> result = await sender.Send(request with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<PaginationViewModel<ReviewTradeViewModel>>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get trades for a review period.")
            .WithDescription("Retrieves paginated trade histories with full journal context for the specified date range.")
            .WithTags(Tags.Reviews)
            .RequireAuthorization();
        }
    }
}
