using Mapster;
using TradingJournal.Shared.Common;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Trades.Features.V1.Trade;

public class GetTrades
{
    public class Request : IQuery<Result<PaginationViewModel<TradeHistoryViewModel>>>
    {
        public string? Asset { get; set; }

        public PositionType? Position { get; set; }

        public TradeStatus? Status { get; set; }

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 10;
        
        public int UserId { get; set; }
    }

    public sealed class Validator : AbstractValidator<Request>
    {
        private const int MaxPageSize = 100;

        public Validator()
        {
            RuleFor(x => x.Page)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Page must be greater than 0.");

            RuleFor(x => x.PageSize)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Page size must be greater than 0.")
                .LessThanOrEqualTo(MaxPageSize)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage($"Page size must not exceed {MaxPageSize}.");
        }
    }

    public sealed class Handler(ITradeDbContext tradeDbContext, ICacheRepository cacheRepository, IEmotionTagProvider emotionTagProvider) : IQueryHandler<Request, Result<PaginationViewModel<TradeHistoryViewModel>>>
    {
        public async Task<Result<PaginationViewModel<TradeHistoryViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            PaginationViewModel<TradeHistoryViewModel> result = await GetTradesFromDatabase(request, cancellationToken);

            return Result<PaginationViewModel<TradeHistoryViewModel>>.Success(result);
        }

        private async Task<PaginationViewModel<TradeHistoryViewModel>> GetTradesFromDatabase(Request request, CancellationToken cancellationToken)
        {
            IQueryable<TradeHistory> query = tradeDbContext.TradeHistories
                .Where(th => th.CreatedBy == request.UserId)
                .AsNoTracking();

            if (!string.IsNullOrEmpty(request.Asset))
            {
                query = query.Where(th => th.Asset.Contains(request.Asset));
            }

            if (request.Position.HasValue)
            {
                query = query.Where(th => th.Position == request.Position.Value);
            }

            if (request.Status.HasValue)
            {
                query = query.Where(th => th.Status == request.Status.Value);
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(th => th.Date >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(th => th.Date <= request.ToDate.Value);
            }

            int totalItems = await query.CountAsync(cancellationToken);

            List<TradeHistory> tradeHistories = await query
                .OrderByDescending(th => th.Date)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            IReadOnlyCollection<TradeHistoryViewModel> tradeHistoryViewModels = tradeHistories.Adapt<IReadOnlyCollection<TradeHistoryViewModel>>();

            // Batch-fetch EmotionTagIds for all trades on this page (single query, no N+1)
            List<int> tradeIds = [.. tradeHistories.Select(t => t.Id)];

            List<TradeEmotionTag> tradeEmotionTags = await tradeDbContext.TradeEmotionTags
                .AsNoTracking()
                .Where(tet => tradeIds.Contains(tet.TradeHistoryId))
                .ToListAsync(cancellationToken);

            // Resolve EmotionTag names via shared provider (auto-populates cache on miss)
            List<EmotionTagCacheDto> cachedEmotionTags = await emotionTagProvider.GetEmotionTagsAsync(cancellationToken);

            Dictionary<int, string> emotionTagLookup = cachedEmotionTags
                .ToDictionary(e => e.Id, e => e.Name);

            // Group EmotionTagIds by TradeHistoryId
            ILookup<int, int> emotionTagIdsByTrade = tradeEmotionTags
                .ToLookup(tet => tet.TradeHistoryId, tet => tet.EmotionTagId);

            foreach (TradeHistoryViewModel viewModel in tradeHistoryViewModels)
            {
                // Resolve EmotionTagIds to names via cache lookup
                viewModel.EmotionTags = [.. emotionTagIdsByTrade[viewModel.Id]
                    .Where(emotionTagLookup.ContainsKey)
                    .Select(x => new EmotionTagCacheDto
                    {
                        Id = x,
                        Name = emotionTagLookup[x],
                        EmotionType = cachedEmotionTags.First(et => et.Id == x).EmotionType
                    })];
            }

            PaginationViewModel<TradeHistoryViewModel> result = new()
            {
                TotalItems = totalItems,
                HasMore = (request.Page * request.PageSize) < totalItems,
                Values = tradeHistoryViewModels
            };

            return result;
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradeHistory);

            group.MapGet("/", async (
                ISender sender,
                ClaimsPrincipal user,
                [FromQuery] string? asset,
                [FromQuery] PositionType? position,
                [FromQuery] TradeStatus? status,
                [FromQuery] DateTime? fromDate,
                [FromQuery] DateTime? toDate,
                [FromQuery] int page = 1,
                [FromQuery] int pageSize = 10) =>
            {
                Request request = new()
                {
                    Asset = asset,
                    Position = position,
                    Status = status,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Page = page,
                    PageSize = pageSize,
                    UserId = user.GetCurrentUserId()
                };

                Result<PaginationViewModel<TradeHistoryViewModel>> result = await sender.Send(request);

                return result.IsSuccess ? Results.Ok(result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<PaginationViewModel<TradeHistoryViewModel>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Get trade histories.")
            .WithDescription("Retrieves a paginated list of trade histories with optional filters.")
            .WithTags(Tags.TradeHistory)
            .RequireAuthorization();
        }
    }
}