namespace TradingJournal.Modules.Trades.Features.V1.Lessons;

public sealed class GetLessonDetail
{
    public sealed record Request(int Id, int UserId = 0) : IQuery<Result<LessonLearnedDetailViewModel>>;

    public sealed class Handler(ITradeDbContext context)
        : IQueryHandler<Request, Result<LessonLearnedDetailViewModel>>
    {
        public async Task<Result<LessonLearnedDetailViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            LessonLearned? lesson = await context.LessonsLearned
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == request.Id && l.CreatedBy == request.UserId, cancellationToken);

            if (lesson is null)
            {
                return Result<LessonLearnedDetailViewModel>.Failure(Error.Create("Lesson not found."));
            }

            // Fetch linked trades with trade details
            List<LinkedTradeViewModel> linkedTrades = await context.LessonTradeLinks
                .AsNoTracking()
                .Where(ltl => ltl.LessonLearnedId == lesson.Id)
                .Select(ltl => new LinkedTradeViewModel
                {
                    Id = ltl.TradeHistory.Id,
                    Asset = ltl.TradeHistory.Asset,
                    Position = ltl.TradeHistory.Position,
                    EntryPrice = ltl.TradeHistory.EntryPrice,
                    ExitPrice = ltl.TradeHistory.ExitPrice,
                    Pnl = ltl.TradeHistory.Pnl,
                    TradingResult = ltl.TradeHistory.TradingResult,
                    Date = ltl.TradeHistory.Date,
                    IsRuleBroken = ltl.TradeHistory.IsRuleBroken
                })
                .OrderByDescending(t => t.Date)
                .ToListAsync(cancellationToken);

            LessonLearnedDetailViewModel viewModel = new()
            {
                Id = lesson.Id,
                Title = lesson.Title,
                Content = lesson.Content,
                Category = lesson.Category,
                Severity = lesson.Severity,
                Status = lesson.Status,
                KeyTakeaway = lesson.KeyTakeaway,
                ActionItems = lesson.ActionItems,
                ImpactScore = lesson.ImpactScore,
                CreatedDate = lesson.CreatedDate,
                UpdatedDate = lesson.UpdatedDate,
                LinkedTrades = linkedTrades
            };

            return Result<LessonLearnedDetailViewModel>.Success(viewModel);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Lessons);

            group.MapGet("/{id:int}", async (int id, ClaimsPrincipal user, ISender sender) =>
            {
                Result<LessonLearnedDetailViewModel> result = await sender.Send(new Request(id) with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .Produces<Result<LessonLearnedDetailViewModel>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Get lesson detail.")
            .WithDescription("Retrieves a lesson with full content and linked trade information.")
            .WithTags(Tags.Lessons)
            .RequireAuthorization();
        }
    }
}
