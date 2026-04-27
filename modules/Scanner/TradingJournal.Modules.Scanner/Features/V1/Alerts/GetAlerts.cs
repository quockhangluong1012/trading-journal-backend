using TradingJournal.Modules.Scanner.Dto;

namespace TradingJournal.Modules.Scanner.Features.V1.Alerts;

public sealed class GetAlerts
{
    public record Request() : IQuery<Result<List<ScannerAlertDto>>>
    {
        public int UserId { get; set; }
        public bool ActiveOnly { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    internal sealed class Handler(IScannerDbContext context)
        : IQueryHandler<Request, Result<List<ScannerAlertDto>>>
    {
        public async Task<Result<List<ScannerAlertDto>>> Handle(Request request, CancellationToken cancellationToken)
        {
            IQueryable<ScannerAlert> query = context.ScannerAlerts
                .Where(a => a.UserId == request.UserId && !a.IsDisabled);

            if (request.ActiveOnly)
            {
                query = query.Where(a => !a.IsDismissed);
            }

            List<ScannerAlertDto> alerts = await query
                .OrderByDescending(a => a.DetectedAt)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(a => new ScannerAlertDto(
                    a.Id,
                    a.Symbol,
                    a.PatternType.ToString(),
                    a.Timeframe.ToString(),
                    a.DetectionTimeframe.ToString(),
                    a.PriceAtDetection,
                    a.ZoneHighPrice,
                    a.ZoneLowPrice,
                    a.Description,
                    a.ConfluenceScore,
                    a.DetectedAt,
                    a.IsDismissed))
                .ToListAsync(cancellationToken);

            return Result<List<ScannerAlertDto>>.Success(alerts);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Alerts);

            group.MapGet("/", async (
                ClaimsPrincipal user,
                ISender sender,
                bool? activeOnly,
                int? page,
                int? pageSize) =>
            {
                var request = new Request
                {
                    UserId = user.GetCurrentUserId(),
                    ActiveOnly = activeOnly ?? true,
                    Page = Math.Max(1, page ?? 1),
                    PageSize = Math.Clamp(pageSize ?? 20, 1, 50)
                };

                Result<List<ScannerAlertDto>> result = await sender.Send(request);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<List<ScannerAlertDto>>>(StatusCodes.Status200OK)
            .WithSummary("Get scanner alerts for the current user.")
            .WithTags(Tags.Alerts)
            .RequireAuthorization();
        }
    }
}
