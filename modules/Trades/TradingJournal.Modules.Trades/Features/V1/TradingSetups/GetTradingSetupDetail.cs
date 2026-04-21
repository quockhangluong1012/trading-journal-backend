namespace TradingJournal.Modules.Trades.Features.V1.TradingSetups;

public sealed class GetTradingSetupDetail
{
    public record Request(int Id, int UserId = 0) : ICommand<Result<TradingSetupDetailViewModel>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Id must be greater than 0.");
        }
    }

    public sealed class Handler(ITradeDbContext context) : ICommandHandler<Request, Result<TradingSetupDetailViewModel>>
    {
        public async Task<Result<TradingSetupDetailViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            if (request.UserId <= 0)
            {
                return Result<TradingSetupDetailViewModel>.Failure(Error.Create("Current user is required."));
            }

            TradingSetup? tradingSetup = await context.TradingSetups
                .AsNoTracking()
                .Include(setup => setup.Steps)
                .Include(setup => setup.Connections)
                .FirstOrDefaultAsync(setup => setup.Id == request.Id && setup.CreatedBy == request.UserId, cancellationToken);

            if (tradingSetup is null)
            {
                return Result<TradingSetupDetailViewModel>.Failure(Error.Create($"Trading setup with id {request.Id} not found."));
            }

            List<SetupStep> orderedSteps = tradingSetup.Steps.OrderBy(step => step.StepNumber).ToList();
            IReadOnlyDictionary<int, string> nodeIdByStepId = orderedSteps
                .ToDictionary(step => step.Id, step => $"setup-step-{step.Id}");

            TradingSetupDetailViewModel viewModel = new(
                tradingSetup.Id,
                tradingSetup.Name,
                tradingSetup.Description,
                TradingSetupDiagram.CountActionableSteps(orderedSteps),
                tradingSetup.CreatedDate,
                tradingSetup.UpdatedDate ?? tradingSetup.CreatedDate,
                orderedSteps.Select(TradingSetupDiagram.ToNodeDto).ToList(),
                tradingSetup.Connections
                    .OrderBy(connection => connection.Id)
                    .Select(connection => TradingSetupDiagram.ToEdgeDto(connection, nodeIdByStepId))
                    .ToList());

            return Result<TradingSetupDetailViewModel>.Success(viewModel);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradingSetups);

            group.MapGet("/{id:int}", async (int id, ISender sender) =>
            {
                Result<TradingSetupDetailViewModel> result = await sender.Send(new Request(id));
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .Produces<Result<TradingSetupDetailViewModel>>()
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Get the details for a single trading setup flow chart.")
            .WithTags(Tags.TradingSetups)
            .RequireAuthorization();
        }
    }
}