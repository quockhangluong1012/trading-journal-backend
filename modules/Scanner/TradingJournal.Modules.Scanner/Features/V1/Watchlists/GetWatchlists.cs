using TradingJournal.Modules.Scanner.Dto;

namespace TradingJournal.Modules.Scanner.Features.V1.Watchlists;

public sealed class GetWatchlists
{
    public record Request() : IQuery<Result<List<WatchlistDto>>>
    {
        public int UserId { get; set; }
    }

    internal sealed class Handler(IScannerDbContext context)
        : IQueryHandler<Request, Result<List<WatchlistDto>>>
    {
        public async Task<Result<List<WatchlistDto>>> Handle(Request request, CancellationToken cancellationToken)
        {
            List<WatchlistDto> watchlists = await context.Watchlists
                .Where(w => w.UserId == request.UserId && !w.IsDisabled)
                .Include(w => w.Assets.Where(a => !a.IsDisabled))
                    .ThenInclude(a => a.EnabledDetectors.Where(d => !d.IsDisabled))
                .OrderByDescending(w => w.CreatedDate)
                .Select(w => new WatchlistDto(
                    w.Id,
                    w.Name,
                    w.IsActive,
                    w.IsScannerRunning,
                    w.CreatedDate,
                    w.Assets.Select(a => new WatchlistAssetDto(
                        a.Id, a.Symbol, a.DisplayName,
                        a.EnabledDetectors.Where(d => d.IsEnabled).Select(d => d.PatternType.ToString()).ToList()
                    )).ToList()))
                .ToListAsync(cancellationToken);

            return Result<List<WatchlistDto>>.Success(watchlists);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Watchlists);

            group.MapGet("/", async (ClaimsPrincipal user, ISender sender) =>
            {
                Result<List<WatchlistDto>> result = await sender.Send(
                    new Request { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<List<WatchlistDto>>>(StatusCodes.Status200OK)
            .WithSummary("Get all watchlists for the current user.")
            .WithTags(Tags.Watchlists)
            .RequireAuthorization();
        }
    }
}
