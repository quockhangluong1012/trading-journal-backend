namespace TradingJournal.Modules.Backtest.Features.V1.Admin;

/// <summary>
/// Admin endpoint to register a new asset for backtesting.
/// After creation, the DataSyncBackgroundService picks it up
/// and begins downloading M1 candles month-by-month.
/// </summary>
public static class CreateAsset
{
    public sealed record Request(
        string DisplayName,
        string Symbol,
        string Category,
        string DataProvider,
        DateTime DataStartDate,
        DateTime? DataEndDate,
        decimal DefaultSpreadPips = 1.0m,
        AssetPipType PipType = AssetPipType.Standard) : ICommand<Result<int>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Symbol).NotEmpty().MaximumLength(30);
            RuleFor(x => x.Category).NotEmpty().Must(c =>
                c is "Forex" or "Metals" or "Futures" or "Crypto" or "Indices")
                .WithMessage("Category must be one of: Forex, Metals, Futures, Crypto, Indices");
            RuleFor(x => x.DataProvider).NotEmpty().Must(p =>
                p is "TwelveData" or "AlphaVantage" or "CSV")
                .WithMessage("DataProvider must be one of: TwelveData, AlphaVantage, CSV");
            RuleFor(x => x.DataStartDate).LessThan(x => x.DataEndDate ?? DateTime.UtcNow);
            RuleFor(x => x.PipType)
                .Must(Enum.IsDefined).WithMessage("Invalid pip type. Valid values: Standard, JpyPair, Metal, Crypto, Index, WholePip");
        }
    }

    internal sealed class Handler(IBacktestDbContext context) : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            // Check for duplicate
            bool exists = await context.BacktestAssets
                .AnyAsync(a => a.Symbol == request.Symbol, cancellationToken);

            if (exists)
                return Result<int>.Failure(new Error("Asset.Duplicate",
                    $"Asset with symbol '{request.Symbol}' already exists."));

            BacktestAsset asset = new()
            {
                Id = 0,
                DisplayName = request.DisplayName,
                Symbol = request.Symbol,
                Category = request.Category,
                DataProvider = request.DataProvider,
                DataStartDate = request.DataStartDate,
                DataEndDate = request.DataEndDate,
                DefaultSpreadPips = request.DefaultSpreadPips,
                PipSize = request.PipType.ToPipSize(),
                SyncStatus = request.DataProvider == "CSV"
                    ? AssetSyncStatus.Pending
                    : AssetSyncStatus.Pending, // BackgroundService picks up Pending status
                TotalCandles = 0
            };

            context.BacktestAssets.Add(asset);
            await context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(asset.Id);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost(AdminApiGroup.V1.BacktestAdmin, async (Request request, ISender sender) =>
            {
                Result<int> result = await sender.Send(request);
                return result.IsSuccess
                    ? Results.Created($"{AdminApiGroup.V1.BacktestAdmin}/{result.Value}", result)
                    : Results.BadRequest(result);
            })
            .WithTags(Tags.BacktestAdmin)
            .WithDescription("Register a new asset for backtesting. Triggers background data sync.")
            .Produces<Result<int>>(StatusCodes.Status201Created)
            .Produces<Result<int>>(StatusCodes.Status400BadRequest)
            .RequireAuthorization("AdminOnly");
        }
    }
}
