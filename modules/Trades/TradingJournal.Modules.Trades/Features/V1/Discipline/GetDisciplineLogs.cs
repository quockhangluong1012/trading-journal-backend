using TradingJournal.Shared.Common;

namespace TradingJournal.Modules.Trades.Features.V1.Discipline;

public sealed class GetDisciplineLogs
{
    public class Request : IQuery<Result<PaginationViewModel<DisciplineLogViewModel>>>
    {
        public int? DisciplineRuleId { get; set; }
        public int? TradeHistoryId { get; set; }
        public bool? WasFollowed { get; set; }
        public DateTimeOffset? FromDate { get; set; }
        public DateTimeOffset? ToDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int UserId { get; set; }
    }

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Page).GreaterThan(0);
            RuleFor(x => x.PageSize).GreaterThan(0);
        }
    }

    public sealed class Handler(ITradeDbContext context)
        : IQueryHandler<Request, Result<PaginationViewModel<DisciplineLogViewModel>>>
    {
        public async Task<Result<PaginationViewModel<DisciplineLogViewModel>>> Handle(
            Request request, CancellationToken cancellationToken)
        {
            IQueryable<DisciplineLog> query = context.DisciplineLogs
                .Include(dl => dl.DisciplineRule)
                .Include(dl => dl.TradeHistory)
                .Where(dl => dl.CreatedBy == request.UserId)
                .AsNoTracking();

            if (request.DisciplineRuleId.HasValue)
                query = query.Where(dl => dl.DisciplineRuleId == request.DisciplineRuleId.Value);

            if (request.TradeHistoryId.HasValue)
                query = query.Where(dl => dl.TradeHistoryId == request.TradeHistoryId.Value);

            if (request.WasFollowed.HasValue)
                query = query.Where(dl => dl.WasFollowed == request.WasFollowed.Value);

            if (request.FromDate.HasValue)
                query = query.Where(dl => dl.Date >= request.FromDate.Value);

            if (request.ToDate.HasValue)
                query = query.Where(dl => dl.Date <= request.ToDate.Value);

            int totalItems = await query.CountAsync(cancellationToken);

            List<DisciplineLogViewModel> logs = await query
                .OrderByDescending(dl => dl.Date)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(dl => new DisciplineLogViewModel
                {
                    Id = dl.Id,
                    DisciplineRuleId = dl.DisciplineRuleId,
                    RuleName = dl.DisciplineRule.Name,
                    TradeHistoryId = dl.TradeHistoryId,
                    TradeAsset = dl.TradeHistory != null ? dl.TradeHistory.Asset : null,
                    WasFollowed = dl.WasFollowed,
                    Notes = dl.Notes,
                    Date = dl.Date
                })
                .ToListAsync(cancellationToken);

            return Result<PaginationViewModel<DisciplineLogViewModel>>.Success(
                new PaginationViewModel<DisciplineLogViewModel>
                {
                    TotalItems = totalItems,
                    HasMore = (request.Page * request.PageSize) < totalItems,
                    Values = logs
                });
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Discipline);

            group.MapPost("/log/search", async ([FromBody] Request request, ClaimsPrincipal user, ISender sender) =>
            {
                request.UserId = user.GetCurrentUserId();
                var result = await sender.Send(request);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<PaginationViewModel<DisciplineLogViewModel>>>(StatusCodes.Status200OK)
            .WithSummary("Search discipline logs.")
            .WithDescription("Paginated search of discipline check history with optional filters.")
            .WithTags(Tags.Discipline)
            .RequireAuthorization();
        }
    }
}
