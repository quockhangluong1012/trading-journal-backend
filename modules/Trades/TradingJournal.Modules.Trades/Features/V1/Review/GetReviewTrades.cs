using TradingJournal.Shared.Common;

namespace TradingJournal.Modules.Trades.Features.V1.Review;

public sealed class GetReviewTrades
{
    internal sealed record Request(
        DateTime FromDate,
        DateTime ToDate,
        int Page = 1,
        int PageSize = 50,
        int UserId = 0) : IQuery<Result<PaginationViewModel<ReviewTradeViewModel>>>;

    internal sealed record ReviewTradeViewModel(
        int Id,
        string Asset,
        string Position,
        double? Pnl,
        DateTime Date,
        DateTime? ClosedDate,
        double EntryPrice,
        double? ExitPrice);

    internal sealed class Validator : AbstractValidator<Request>
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

    internal sealed class Handler(ITradeDbContext context) : IQueryHandler<Request, Result<PaginationViewModel<ReviewTradeViewModel>>>
    {
        public async Task<Result<PaginationViewModel<ReviewTradeViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            DateTime fromDate = request.FromDate.Date;
            DateTime toDate = request.ToDate.Date.AddDays(1).AddTicks(-1); // Include the entire ToDate day

            IQueryable<TradeHistory> query = context.TradeHistories
                .AsNoTracking()
                .Where(th => th.CreatedBy == request.UserId)
                .Where(th => th.Status == TradeStatus.Closed && th.Pnl.HasValue)
                .Where(th => th.ClosedDate.HasValue && th.ClosedDate.Value >= fromDate && th.ClosedDate.Value <= toDate);

            int totalItems = await query.CountAsync(cancellationToken);

            List<ReviewTradeViewModel> trades = await query
                .OrderByDescending(th => th.ClosedDate)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(th => new ReviewTradeViewModel(
                    th.Id,
                    th.Asset,
                    th.Position.ToString(),
                    th.Pnl,
                    th.Date,
                    th.ClosedDate,
                    th.EntryPrice,
                    th.ExitPrice))
                .ToListAsync(cancellationToken);

            PaginationViewModel<ReviewTradeViewModel> result = new()
            {
                TotalItems = totalItems,
                HasMore = (request.Page * request.PageSize) < totalItems,
                Values = trades
            };

            return Result<PaginationViewModel<ReviewTradeViewModel>>.Success(result);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Reviews);

            group.MapPost("/trades", async (ISender sender, [FromBody] Request request) =>
            {
                Result<PaginationViewModel<ReviewTradeViewModel>> result = await sender.Send(request);

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<PaginationViewModel<ReviewTradeViewModel>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get trades for a review period.")
            .WithDescription("Retrieves paginated trade histories for the specified date range.")
            .WithTags(Tags.Reviews)
            .RequireAuthorization();
        }
    }
}
