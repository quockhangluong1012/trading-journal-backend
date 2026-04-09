using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Modules.Backtest.Events;

namespace TradingJournal.Modules.Backtest.Features.V1.Sessions;

public sealed class CreateSession
{
    public record Request(
        string Asset,
        DateTime StartDate,
        DateTime? EndDate,
        decimal InitialBalance) : ICommand<Result<int>>
    {
        public int UserId { get; set; }
    }

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Asset)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Asset pair must be provided (e.g., BTC/USDT).");

            RuleFor(x => x.StartDate)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Start date must be provided.")
                .LessThan(DateTime.UtcNow).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Start date must be in the past for backtesting.");

            RuleFor(x => x.InitialBalance)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Initial balance must be greater than 0.");
        }
    }

    internal sealed class Handler(IBacktestDbContext context, IEventBus eventBus) : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            try
            {
                DateTime endDate = request.EndDate ?? DateTime.UtcNow;
                string normalizedAsset = request.Asset.Trim().ToUpperInvariant();

                // Look up asset to get spread configuration
                BacktestAsset? asset = await context.BacktestAssets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Symbol == normalizedAsset, cancellationToken);

                decimal spread = asset is not null
                    ? asset.DefaultSpreadPips * asset.PipSize
                    : 0m;

                BacktestSession session = new()
                {
                    Id = 0,
                    Asset = normalizedAsset,
                    StartDate = request.StartDate,
                    EndDate = endDate,
                    InitialBalance = request.InitialBalance,
                    CurrentBalance = request.InitialBalance,
                    Status = BacktestSessionStatus.InProgress,
                    CurrentTimestamp = request.StartDate,
                    ActiveTimeframe = Timeframe.M15,
                    PlaybackSpeed = 1,
                    Spread = spread,
                    IsDataReady = false
                };

                await context.BacktestSessions.AddAsync(session, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);

                // Publish event to trigger market data download
                await eventBus.PublishAsync(new FetchHistoricalDataEvent(
                    Guid.NewGuid(),
                    session.Id,
                    session.Asset,
                    session.StartDate,
                    endDate,
                    request.UserId), cancellationToken);

                return Result<int>.Success(session.Id);
            }
            catch (Exception ex)
            {
                return Result<int>.Failure(Error.Create(ex.Message));
            }
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Sessions);

            group.MapPost("/", async ([FromBody] Request request, ISender sender) =>
            {
                Result<int> result = await sender.Send(request);

                return result.IsSuccess
                    ? Results.Created($"{ApiGroup.V1.Sessions}/{result.Value}", result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<int>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Create a new backtest session.")
            .WithDescription("Creates a new backtest session, triggers historical data download, and returns the session ID.")
            .WithTags(Tags.BacktestSessions)
            .RequireAuthorization();
        }
    }
}
