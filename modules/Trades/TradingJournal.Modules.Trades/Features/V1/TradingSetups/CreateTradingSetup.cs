namespace TradingJournal.Modules.Trades.Features.V1.TradingSetups;

public sealed class CreateTradingSetup
{
    public record Request(
        string Name,
        string? Description,
        IReadOnlyCollection<TradingSetupNodeDto> Nodes,
        IReadOnlyCollection<TradingSetupEdgeDto> Edges,
        int UserId = 0) : ICommand<Result<int>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
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

    public sealed class Handler(ITradeDbContext context) : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            if (request.UserId <= 0)
            {
                return Result<int>.Failure(Error.Create("Current user is required."));
            }

            List<SetupStep> steps = TradingSetupDiagram.BuildSteps(request.Nodes);
            IReadOnlyDictionary<string, SetupStep> stepsByNodeId = TradingSetupDiagram.MapStepsByNodeId(request.Nodes, steps);
            List<SetupConnection> connections = TradingSetupDiagram.BuildConnections(request.Edges, stepsByNodeId);

            TradingSetup tradingSetup = new()
            {
                Id = 0,
                Name = request.Name.Trim(),
                Model = "flowchart",
                Description = TradingSetupDiagram.NormalizeOptionalText(request.Description),
                Status = SetupStatus.Active,
                Notes = null,
                Steps = steps,
                Connections = connections,
            };

            await context.TradingSetups.AddAsync(tradingSetup, cancellationToken);

            int createdRows = await context.SaveChangesAsync(cancellationToken);

            return createdRows > 0
                ? Result<int>.Success(tradingSetup.Id)
                : Result<int>.Failure(Error.Create("Failed to create trading setup."));
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradingSetups);

            group.MapPost("/", async ([FromBody] Request request, ClaimsPrincipal user, ISender sender) =>
            {
                Result<int> result = await sender.Send(request with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess ? Results.Created($"{ApiGroup.V1.TradingSetups}/{result.Value}", result) : Results.BadRequest(result);
            })
            .Produces<Result<int>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Create a trading setup flow chart.")
            .WithTags(Tags.TradingSetups)
            .RequireAuthorization();
        }
    }
}