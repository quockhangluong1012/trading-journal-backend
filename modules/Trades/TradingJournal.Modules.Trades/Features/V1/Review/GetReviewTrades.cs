using TradingJournal.Shared.Common;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Trades.Features.V1.Review;

public sealed class GetReviewTrades
{
    public sealed class Request : IQuery<Result<PaginationViewModel<ReviewTradeViewModel>>>, IUserAwareRequest
    {
        public DateTime FromDate { get; set; }

        public DateTime ToDate { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 50;

        public int UserId { get; set; }
    }

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
        List<string> EmotionTags,
        List<string> TechnicalThemes,
        List<string> ChecklistItems);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.FromDate)
                .NotEmpty()
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("FromDate is required.");

            RuleFor(x => x.ToDate)
                .NotEmpty()
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("ToDate is required.");

            RuleFor(x => x.Page)
                .GreaterThan(0)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Page must be greater than 0.");

            RuleFor(x => x.PageSize)
                .GreaterThan(0).WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("PageSize must be greater than 0.")
                .LessThanOrEqualTo(100).WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("PageSize must not exceed 100.");
        }
    }

    public sealed class Handler(
        ITradeDbContext context,
        IEmotionTagProvider emotionTagProvider)
        : IQueryHandler<Request, Result<PaginationViewModel<ReviewTradeViewModel>>>
    {
        public async Task<Result<PaginationViewModel<ReviewTradeViewModel>>> Handle(
            Request request, CancellationToken cancellationToken)
        {
            IQueryable<TradeHistory> query = context.TradeHistories
                .Where(t => t.CreatedBy == request.UserId
                         && t.Date >= request.FromDate
                         && t.Date <= request.ToDate)
                .AsNoTracking();

            int totalItems = await query.CountAsync(cancellationToken);

            List<TradeHistory> trades = await query
                .OrderByDescending(t => t.Date)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Include(t => t.TradingZone)
                .ToListAsync(cancellationToken);

            List<int> tradeIds = [.. trades.Select(t => t.Id)];

            // Batch-load emotion tags
            List<TradeEmotionTag> emotionTags = await context.TradeEmotionTags
                .AsNoTracking()
                .Where(e => tradeIds.Contains(e.TradeHistoryId))
                .ToListAsync(cancellationToken);

            List<EmotionTagCacheDto> cachedEmotionTags = await emotionTagProvider.GetEmotionTagsAsync(cancellationToken);
            Dictionary<int, string> emotionLookup = cachedEmotionTags.ToDictionary(e => e.Id, e => e.Name);
            ILookup<int, int> emotionsByTrade = emotionTags.ToLookup(e => e.TradeHistoryId, e => e.EmotionTagId);

            // Batch-load technical analysis tags
            List<TradeTechnicalAnalysisTag> techTags = await context.TradeTechnicalAnalysisTags
                .AsNoTracking()
                .Where(t => tradeIds.Contains(t.TradeHistoryId))
                .Include(t => t.TechnicalAnalysis)
                .ToListAsync(cancellationToken);

            ILookup<int, string> techThemesByTrade = techTags
                .Where(t => t.TechnicalAnalysis != null)
                .ToLookup(t => t.TradeHistoryId, t => t.TechnicalAnalysis!.Name);

            // Batch-load checklist items
            List<TradeHistoryChecklist> checklists = await context.TradeHistoryChecklist
                .AsNoTracking()
                .Where(c => tradeIds.Contains(c.TradeHistoryId))
                .Include(c => c.PretradeChecklist)
                .ToListAsync(cancellationToken);

            ILookup<int, string> checklistsByTrade = checklists
                .Where(c => c.PretradeChecklist != null)
                .ToLookup(c => c.TradeHistoryId, c => c.PretradeChecklist!.Name);

            // Map to view models
            List<ReviewTradeViewModel> viewModels = [.. trades.Select(t => new ReviewTradeViewModel(
                t.Id,
                t.Asset,
                t.Position.ToString(),
                t.Pnl,
                t.Date,
                t.ClosedDate,
                t.EntryPrice,
                t.ExitPrice,
                (int)t.ConfidenceLevel,
                t.TradingZone?.Name,
                t.IsRuleBroken,
                t.RuleBreakReason,
                t.Notes,
                [.. emotionsByTrade[t.Id]
                    .Where(emotionLookup.ContainsKey)
                    .Select(id => emotionLookup[id])],
                [.. techThemesByTrade[t.Id]],
                [.. checklistsByTrade[t.Id]]))];

            return Result<PaginationViewModel<ReviewTradeViewModel>>.Success(new PaginationViewModel<ReviewTradeViewModel>
            {
                TotalItems = totalItems,
                HasMore = (request.Page * request.PageSize) < totalItems,
                Values = viewModels,
            });
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Reviews);

            group.MapPost("/trades", async ([FromBody] Request request, ClaimsPrincipal user, ISender sender) =>
            {
                request.UserId = user.GetCurrentUserId();
                Result<PaginationViewModel<ReviewTradeViewModel>> result = await sender.Send(request);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<PaginationViewModel<ReviewTradeViewModel>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get trades for a review period.")
            .WithDescription("Retrieves a paginated list of trades within the specified date range for review.")
            .WithTags(Tags.Reviews)
            .RequireAuthorization();
        }
    }
}
