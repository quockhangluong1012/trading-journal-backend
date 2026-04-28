using TradingJournal.Modules.Scanner.Dto;

namespace TradingJournal.Modules.Scanner.Features.V1.Scanner;

public sealed class StopScanner
{
    public record Command() : ICommand<Result<ScannerStatusDto>>
    {
        public int UserId { get; set; }
    }

    internal sealed class Handler(IScannerDbContext context)
        : ICommandHandler<Command, Result<ScannerStatusDto>>
    {
        public async Task<Result<ScannerStatusDto>> Handle(Command request, CancellationToken cancellationToken)
        {
            ScannerConfig? config = await context.ScannerConfigs
                .Include(c => c.EnabledPatterns)
                .Include(c => c.EnabledTimeframes)
                .FirstOrDefaultAsync(c => c.UserId == request.UserId, cancellationToken);

            if (config is null)
                return Result<ScannerStatusDto>.Failure(new Error("ConfigNotFound", "Scanner config not found. Start the scanner first."));

            config.IsRunning = false;

            // Stop scanner for ALL watchlists
            var watchlists = await context.Watchlists
                .Where(w => w.UserId == request.UserId && !w.IsDisabled)
                .ToListAsync(cancellationToken);

            foreach (var w in watchlists)
            {
                w.IsScannerRunning = false;
            }

            await context.SaveChangesAsync(cancellationToken);

            var dto = new ScannerStatusDto(
                ScannerStatus.Stopped.ToString(),
                config.ScanIntervalSeconds,
                config.EnabledPatterns.Select(p => p.PatternType.ToString()).ToList(),
                config.EnabledTimeframes.Select(t => t.Timeframe.ToString()).ToList(),
                config.MinConfluenceScore,
                null,
                0,
                0);

            return Result<ScannerStatusDto>.Success(dto);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Scanner);

            group.MapPost("/stop", async (ClaimsPrincipal user, ISender sender) =>
            {
                Result<ScannerStatusDto> result = await sender.Send(
                    new Command { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<ScannerStatusDto>>(StatusCodes.Status200OK)
            .WithSummary("Stop scanners for ALL watchlists (batch operation).")
            .WithTags(Tags.Scanner)
            .RequireAuthorization();
        }
    }
}
