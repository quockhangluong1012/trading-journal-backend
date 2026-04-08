namespace TradingJournal.Modules.Trades.Features.V1.ChecklistModels;

public sealed class AddCriteriaToModel
{
    internal record Request(int ModelId, string Name, PretradeChecklistType Type) : ICommand<Result<int>>;

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.ModelId)
                .GreaterThan(0).WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Model id must be greater than 0.");

            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Criteria name cannot be null.")
                .NotEmpty().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Criteria name cannot be empty.");

            RuleFor(x => x.Type)
                .Cascade(CascadeMode.Stop)
                .Must(type => Enum.IsDefined(type))
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Criteria type must be a valid PretradeChecklistType value.");
        }
    }

    internal sealed class Handler(ITradeDbContext context) : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            bool modelExists = await context.ChecklistModels
                .AnyAsync(m => m.Id == request.ModelId, cancellationToken);

            if (!modelExists)
                return Result<int>.Failure(Error.Create($"Checklist model with id {request.ModelId} not found."));

            PretradeChecklist criteria = new()
            {
                Id = 0,
                Name = request.Name,
                CheckListType = request.Type,
                ChecklistModelId = request.ModelId
            };

            await context.PretradeChecklists.AddAsync(criteria, cancellationToken);
            int insertedRow = await context.SaveChangesAsync(cancellationToken);

            return insertedRow > 0 ? Result<int>.Success(criteria.Id)
                : Result<int>.Failure(Error.Create("Failed to add criteria to model."));
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.ChecklistModels);

            group.MapPost("/{modelId:int}/criteria", async (int modelId, [FromBody] AddCriteriaRequest body, ISender sender) =>
            {
                Result<int> result = await sender.Send(new Request(modelId, body.Name, body.Type));
                return result.IsSuccess ? Results.Created($"{ApiGroup.V1.ChecklistModels}/{modelId}/criteria/{result.Value}", result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<int>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Add a new criteria to a checklist model.")
            .WithDescription("Adds a new pretrade checklist criteria to the specified model.")
            .WithTags(Tags.ChecklistModels)
            .RequireAuthorization();
        }
    }

    internal record AddCriteriaRequest(string Name, PretradeChecklistType Type);
}
