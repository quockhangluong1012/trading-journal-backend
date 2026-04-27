using TradingJournal.Modules.Scanner.Dto;

namespace TradingJournal.Modules.Scanner.Features.V1.Scanner;

public sealed class StartScanner
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
            {
                // Create default config with all patterns and timeframes enabled
                config = new ScannerConfig
                {
                    Id = default!,
                    UserId = request.UserId,
                    ScanIntervalSeconds = 300,
                    MinConfluenceScore = 1,
                    IsRunning = true,
                    EnabledPatterns =
                    [
                        new() { Id = default!, PatternType = IctPatternType.FVG },
                        new() { Id = default!, PatternType = IctPatternType.OrderBlock },
                        new() { Id = default!, PatternType = IctPatternType.BreakerBlock },
                        new() { Id = default!, PatternType = IctPatternType.Liquidity },
                        new() { Id = default!, PatternType = IctPatternType.LiquiditySweep },
                        new() { Id = default!, PatternType = IctPatternType.InversionFVG },
                        new() { Id = default!, PatternType = IctPatternType.UnicornModel },
                        new() { Id = default!, PatternType = IctPatternType.VenomModel },
                        new() { Id = default!, PatternType = IctPatternType.MitigationBlock },
                        new() { Id = default!, PatternType = IctPatternType.MarketStructureShift },
                        new() { Id = default!, PatternType = IctPatternType.ChangeOfCharacter },
                        new() { Id = default!, PatternType = IctPatternType.Displacement },
                        new() { Id = default!, PatternType = IctPatternType.OptimalTradeEntry },
                        new() { Id = default!, PatternType = IctPatternType.JudasSwing },
                        new() { Id = default!, PatternType = IctPatternType.BalancedPriceRange },
                        new() { Id = default!, PatternType = IctPatternType.CISD },
                        new() { Id = default!, PatternType = IctPatternType.SMTDivergence }
                    ],
                    EnabledTimeframes =
                    [
                        new() { Id = default!, Timeframe = ScannerTimeframe.D1 },
                        new() { Id = default!, Timeframe = ScannerTimeframe.H1 },
                        new() { Id = default!, Timeframe = ScannerTimeframe.M15 },
                        new() { Id = default!, Timeframe = ScannerTimeframe.M5 }
                    ]
                };

                context.ScannerConfigs.Add(config);
            }
            else
            {
                config.IsRunning = true;
            }

            await context.SaveChangesAsync(cancellationToken);

            int watchlistCount = await context.Watchlists
                .CountAsync(w => w.UserId == request.UserId && w.IsActive && !w.IsDisabled, cancellationToken);

            int assetCount = await context.Watchlists
                .Where(w => w.UserId == request.UserId && w.IsActive && !w.IsDisabled)
                .SelectMany(w => w.Assets)
                .Where(a => !a.IsDisabled)
                .Select(a => a.Symbol)
                .Distinct()
                .CountAsync(cancellationToken);

            var dto = new ScannerStatusDto(
                ScannerStatus.Running.ToString(),
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

            group.MapPost("/start", async (ClaimsPrincipal user, ISender sender) =>
            {
                Result<ScannerStatusDto> result = await sender.Send(
                    new Command { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<ScannerStatusDto>>(StatusCodes.Status200OK)
            .WithSummary("Start the scanner for the current user.")
            .WithTags(Tags.Scanner)
            .RequireAuthorization();
        }
    }
}
