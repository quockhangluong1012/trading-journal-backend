using TradingJournal.Modules.Scanner.Dto;

namespace TradingJournal.Modules.Scanner.Features.V1.Watchlists;

public sealed class StartWatchlistScanner
{
    public record Command() : ICommand<Result<WatchlistDto>>
    {
        public int UserId { get; set; }
        public int WatchlistId { get; set; }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.WatchlistId).GreaterThan(0);
        }
    }

    internal sealed class Handler(IScannerDbContext context)
        : ICommandHandler<Command, Result<WatchlistDto>>
    {
        public async Task<Result<WatchlistDto>> Handle(Command request, CancellationToken cancellationToken)
        {
            Watchlist? watchlist = await context.Watchlists
                .Include(w => w.Assets)
                .FirstOrDefaultAsync(w => w.Id == request.WatchlistId && w.UserId == request.UserId, cancellationToken);

            if (watchlist is null)
                return Result<WatchlistDto>.Failure(new Error("WatchlistNotFound", "Watchlist not found."));

            // Auto-provision ScannerConfig with defaults if not exists
            ScannerConfig? config = await context.ScannerConfigs
                .FirstOrDefaultAsync(c => c.UserId == request.UserId, cancellationToken);

            if (config is null)
            {
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

            watchlist.IsScannerRunning = true;
            watchlist.IsActive = true;
            await context.SaveChangesAsync(cancellationToken);

            var dto = new WatchlistDto(
                watchlist.Id,
                watchlist.Name,
                watchlist.IsActive,
                watchlist.IsScannerRunning,
                watchlist.CreatedDate,
                watchlist.Assets.Select(a => new WatchlistAssetDto(
                    a.Id, a.Symbol, a.DisplayName, new List<string>())).ToList());

            return Result<WatchlistDto>.Success(dto);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Watchlists);

            group.MapPost("/{id:int}/scanner/start", async (int id, ClaimsPrincipal user, ISender sender) =>
            {
                Result<WatchlistDto> result = await sender.Send(
                    new Command { UserId = user.GetCurrentUserId(), WatchlistId = id });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<WatchlistDto>>(StatusCodes.Status200OK)
            .WithSummary("Start the scanner engine for a specific watchlist.")
            .WithTags(Tags.Watchlists)
            .RequireAuthorization();
        }
    }
}
