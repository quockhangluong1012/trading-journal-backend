using TradingJournal.Modules.Scanner.Dto;

namespace TradingJournal.Modules.Scanner.Features.V1.Scanner;

public sealed class GetScannerStatus
{
    public record Request() : IQuery<Result<ScannerStatusDto>>
    {
        public int UserId { get; set; }
    }

    internal sealed class Handler(IScannerDbContext context)
        : IQueryHandler<Request, Result<ScannerStatusDto>>
    {
        public async Task<Result<ScannerStatusDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            ScannerConfig? config = await context.ScannerConfigs
                .Include(c => c.EnabledPatterns)
                .Include(c => c.EnabledTimeframes)
                .FirstOrDefaultAsync(c => c.UserId == request.UserId, cancellationToken);

            if (config is null)
            {
                return Result<ScannerStatusDto>.Success(new ScannerStatusDto(
                    ScannerStatus.Stopped.ToString(),
                    300,
                    [],
                    [],
                    1,
                    null,
                    0,
                    0));
            }

            int watchlistCount = await context.Watchlists
                .CountAsync(w => w.UserId == request.UserId && w.IsActive && !w.IsDisabled, cancellationToken);

            int assetCount = await context.Watchlists
                .Where(w => w.UserId == request.UserId && w.IsActive && !w.IsDisabled)
                .SelectMany(w => w.Assets)
                .Where(a => !a.IsDisabled)
                .Select(a => a.Symbol)
                .Distinct()
                .CountAsync(cancellationToken);

            string status = config.IsRunning
                ? ScannerStatus.Running.ToString()
                : ScannerStatus.Stopped.ToString();

            var dto = new ScannerStatusDto(
                status,
                config.ScanIntervalSeconds,
                config.EnabledPatterns.Select(p => p.PatternType.ToString()).ToList(),
                config.EnabledTimeframes.Select(t => t.Timeframe.ToString()).ToList(),
                config.MinConfluenceScore,
                null,
                watchlistCount,
                assetCount);

            return Result<ScannerStatusDto>.Success(dto);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Scanner);

            group.MapGet("/status", async (ClaimsPrincipal user, ISender sender) =>
            {
                Result<ScannerStatusDto> result = await sender.Send(
                    new Request { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<ScannerStatusDto>>(StatusCodes.Status200OK)
            .WithSummary("Get the current scanner status and configuration.")
            .WithTags(Tags.Scanner)
            .RequireAuthorization();
        }
    }
}
