namespace TradingJournal.Modules.Trades.Features.V1.TradingSession;

public sealed class GetTradeSessions
{
    public sealed record Request(int PageNumber, int PageSize, string? Search, int UserId = 0) : IQuery<Result<List<Domain.TradingSession>>>;

    public sealed class Handler(ITradeDbContext context) : IQueryHandler<Request, Result<List<Domain.TradingSession>>>
    {
        public async Task<Result<List<Domain.TradingSession>>> Handle(Request request, CancellationToken cancellationToken)
        {
            var tradeSessions = await context.TradingSessions
                .Where(x => x.CreatedBy == request.UserId)
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            return Result<List<Domain.TradingSession>>.Success(tradeSessions);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradingSessions);

            group.MapGet("/", async ([FromQuery] int pageNumber, [FromQuery] int pageSize, [FromQuery] string? search, ISender sender) =>
            {
                Result<List<Domain.TradingSession>> result = await sender.Send(new Request(pageNumber, pageSize, search));

                return result.IsSuccess ? Results.Ok(result)
                    : Results.BadRequest(result.Errors);
            })
            .Produces<Result<List<Domain.TradingSession>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Get all trade sessions.")
            .WithDescription("Retrieves all trade sessions.")
            .WithTags(Tags.TradingSessions)
            .RequireAuthorization();
        }
    }
}