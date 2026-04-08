using Mapster;

namespace TradingJournal.Modules.Trades.Features.V1.TechnicalAnalysis;

public sealed class GetTechnicalAnalysisDetail
{
    internal sealed record Request(int Id) : IQuery<Result<TechnicalAnalysisViewModel>>;

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Id)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0).WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Technical Analysis Id must be greater than 0.");
        }
    }

    internal sealed class Handler(ITradeDbContext context) : IQueryHandler<Request, Result<TechnicalAnalysisViewModel>>
    {
        public async Task<Result<TechnicalAnalysisViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            Domain.TechnicalAnalysis? technicalAnalysis = await context.TechnicalAnalyses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

            if (technicalAnalysis is null)
            {
                return Result<TechnicalAnalysisViewModel>.Failure(Error.NotFound);
            }

            TechnicalAnalysisViewModel technicalAnalysisViewModel = technicalAnalysis.Adapt<TechnicalAnalysisViewModel>();

            return Result<TechnicalAnalysisViewModel>.Success(technicalAnalysisViewModel);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TechnicalAnalysis);

            group.MapGet("/{id:int}", async (int id, ISender sender) =>
            {
                Result<TechnicalAnalysisViewModel> result = await sender.Send(new Request(id));

                return result.IsSuccess ? Results.Ok(result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<TechnicalAnalysisViewModel>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Get a technical analysis by ID.")
            .WithDescription("Gets a technical analysis by its ID.")
            .WithTags(Tags.TechnicalAnalysis)
            .RequireAuthorization();
        }
    }
}