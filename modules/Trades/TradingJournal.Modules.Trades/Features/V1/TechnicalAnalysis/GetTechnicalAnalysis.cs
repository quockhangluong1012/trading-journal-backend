using Mapster;

namespace TradingJournal.Modules.Trades.Features.V1.TechnicalAnalysis;

public sealed class GetTechnicalAnalysis
{
    public sealed record Request() : IQuery<Result<IReadOnlyCollection<TechnicalAnalysisViewModel>>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
        }
    }

    public sealed class Handler(ITradeDbContext context) : IQueryHandler<Request, Result<IReadOnlyCollection<TechnicalAnalysisViewModel>>>
    {
        public async Task<Result<IReadOnlyCollection<TechnicalAnalysisViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<Domain.TechnicalAnalysis> technicalAnalyses = await context.TechnicalAnalyses
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            IReadOnlyCollection<TechnicalAnalysisViewModel> technicalAnalysisViewModels = technicalAnalyses.Adapt<IReadOnlyCollection<TechnicalAnalysisViewModel>>();

            return Result<IReadOnlyCollection<TechnicalAnalysisViewModel>>.Success(technicalAnalysisViewModels);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TechnicalAnalysis);

            group.MapGet("/", async (ISender sender) =>
            {
                Result<IReadOnlyCollection<TechnicalAnalysisViewModel>> result = await sender.Send(new Request());

                return result.IsSuccess ? Results.Ok(result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<IReadOnlyCollection<TechnicalAnalysisViewModel>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Get all technical analyses.")
            .WithDescription("Gets all technical analyses.")
            .WithTags(Tags.TechnicalAnalysis)
            .RequireAuthorization();
        }
    }
}