using TradingJournal.Shared.Common;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.AiInsights.Features.V1.Review;

public sealed class GetReviewTrades
{
    public sealed class Request : IQuery<Result<PaginationViewModel<ReviewTradeDto>>>, IUserAwareRequest
    {
        public DateTime FromDate { get; set; }

        public DateTime ToDate { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 50;

        public int UserId { get; set; }
    }

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

    public sealed class Handler(IAiTradeDataProvider tradeDataProvider)
        : IQueryHandler<Request, Result<PaginationViewModel<ReviewTradeDto>>>
    {
        public async Task<Result<PaginationViewModel<ReviewTradeDto>>> Handle(
            Request request, CancellationToken cancellationToken)
        {
            ReviewTradesPageDto page = await tradeDataProvider.GetReviewTradesAsync(
                request.FromDate, request.ToDate, request.UserId,
                request.Page, request.PageSize, cancellationToken);

            return Result<PaginationViewModel<ReviewTradeDto>>.Success(new PaginationViewModel<ReviewTradeDto>
            {
                TotalItems = page.TotalCount,
                HasMore = page.HasMore,
                Values = page.Items,
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
                Result<PaginationViewModel<ReviewTradeDto>> result = await sender.Send(request);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<PaginationViewModel<ReviewTradeDto>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get trades for a review period.")
            .WithDescription("Retrieves a paginated list of trades within the specified date range for review.")
            .WithTags(Tags.Reviews)
            .RequireAuthorization();
        }
    }
}
