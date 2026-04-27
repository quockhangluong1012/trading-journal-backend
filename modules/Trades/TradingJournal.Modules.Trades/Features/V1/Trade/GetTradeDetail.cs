using Mapster;

namespace TradingJournal.Modules.Trades.Features.V1.Trade;

public class GetTradeDetail
{
    public record Request(int Id, int UserId = 0) : IQuery<Result<TradeHistoryDetailViewModel>>;

    public class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Id)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Trade ID must be greater than 0.");
        }
    }   

    public class Handler(ITradeDbContext tradeDbContext) : IQueryHandler<Request, Result<TradeHistoryDetailViewModel>>
    {
        public async Task<Result<TradeHistoryDetailViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            TradeHistory? trade = await tradeDbContext.TradeHistories
                .AsNoTracking()
                .Include(x => x.TradeScreenShots)
                .Include(x => x.TradeEmotionTags)
                .Include(x => x.TradeChecklists)
                .Include(x => x.TradeTechnicalAnalysisTags)
                .FirstOrDefaultAsync(x => x.Id == request.Id && x.CreatedBy == request.UserId, cancellationToken: cancellationToken);

            if (trade == null)
            {
                return Result<TradeHistoryDetailViewModel>.Failure(Error.NotFound);
            }

            TradeHistoryDetailViewModel tradeHistoryDetailViewModel = new()
            {
                Asset = trade.Asset,
                Position = trade.Position,
                EntryPrice = trade.EntryPrice,
                Date = trade.Date,
                Status = trade.Status,
                ExitPrice = trade.ExitPrice,
                Pnl = trade.Pnl,
                ClosedDate = trade.ClosedDate,
                TradingResult = trade.TradingResult,
                HitStopLoss = trade.HitStopLoss,
                Notes = trade.Notes,
                TradingSessionId = trade.TradingSessionId,
                TradingZoneId = trade.TradingZoneId,
                TargetTier1 = trade.TargetTier1,
                TargetTier2 = trade.TargetTier2,
                TargetTier3 = trade.TargetTier3,
                StopLoss = trade.StopLoss,
                ConfidenceLevel = trade.ConfidenceLevel,
                EmotionTags = trade.TradeEmotionTags?.Select(x => x.EmotionTagId).ToList(),
                ScreenShots = [.. trade.TradeScreenShots.Select(x => x.Url)],
                SelectedChecklists = [.. trade.TradeChecklists.Select(x => x.PretradeChecklistId)],
                TechnicalAnalysisTags = [.. trade.TradeTechnicalAnalysisTags.Select(x => x.TechnicalAnalysisId)],
            };

            return Result<TradeHistoryDetailViewModel>.Success(tradeHistoryDetailViewModel);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradeHistory);

            group.MapGet("/{id}", async ([FromRoute] int id, ClaimsPrincipal user, ISender sender) => {
                Result<TradeHistoryDetailViewModel> result = await sender.Send(new Request(id) with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) 
                    : Results.BadRequest(result);
            })
            .Produces<Result<TradeHistoryDetailViewModel>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Get a trade history by ID.")
            .WithDescription("Retrieves a trade history by its ID.") 
            .WithTags(Tags.TradeHistory)
            .RequireAuthorization();
        }
    }
}