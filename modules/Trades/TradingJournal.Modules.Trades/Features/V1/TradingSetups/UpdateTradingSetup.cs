namespace TradingJournal.Modules.Trades.Features.V1.TradingSetups;

public sealed class UpdateTradingSetup
{
    public record Request(
        int Id,
        string Name,
        string? Description,
        IReadOnlyCollection<TradingSetupNodeDto> Nodes,
        IReadOnlyCollection<TradingSetupEdgeDto> Edges,
        int UserId = 0) : ICommand<Result<bool>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Setup id must be greater than 0.");

            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Setup name cannot be null.")
                .NotEmpty().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Setup name cannot be empty.");

            RuleFor(x => x)
                .Custom((request, context) =>
                {
                    foreach ((string property, string message) in TradingSetupDiagram.Validate(request.Nodes, request.Edges))
                    {
                        context.AddFailure(property, message);
                    }
                });
        }
    }

    public sealed class Handler(ITradeDbContext context) : ICommandHandler<Request, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Request request, CancellationToken cancellationToken)
        {
            TradingSetup? tradingSetup = await context.TradingSetups
                .Include(setup => setup.Steps)
                .Include(setup => setup.Connections)
                .FirstOrDefaultAsync(setup => setup.Id == request.Id && setup.CreatedBy == request.UserId, cancellationToken);

            if (tradingSetup is null)
            {
                return Result<bool>.Failure(Error.Create($"Trading setup with id {request.Id} not found."));
            }

            context.SetupConnections.RemoveRange(tradingSetup.Connections);
            context.SetupSteps.RemoveRange(tradingSetup.Steps);

            List<SetupStep> steps = TradingSetupDiagram.BuildSteps(request.Nodes);
            IReadOnlyDictionary<string, SetupStep> stepsByNodeId = TradingSetupDiagram.MapStepsByNodeId(request.Nodes, steps);
            List<SetupConnection> connections = TradingSetupDiagram.BuildConnections(request.Edges, stepsByNodeId);

            tradingSetup.Name = request.Name.Trim();
            tradingSetup.Model = "flowchart";
            tradingSetup.Description = TradingSetupDiagram.NormalizeOptionalText(request.Description);
            tradingSetup.Notes = null;
            tradingSetup.Steps = steps;
            tradingSetup.Connections = connections;

            int updatedRows = await context.SaveChangesAsync(cancellationToken);

            return updatedRows > 0
                ? Result<bool>.Success(true)
                : Result<bool>.Failure(Error.Create("Failed to update trading setup."));
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradingSetups);

            group.MapPut("/", async ([FromBody] Request request, ISender sender) =>
            {
                Result<bool> result = await sender.Send(request);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<bool>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Replace the nodes and connections for an existing trading setup flow chart.")
            .WithTags(Tags.TradingSetups)
            .RequireAuthorization();
        }
    }
}