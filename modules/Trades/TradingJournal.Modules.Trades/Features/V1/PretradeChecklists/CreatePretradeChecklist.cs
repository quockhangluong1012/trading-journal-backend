namespace TradingJournal.Modules.Trades.Features.V1.PretradeChecklists;

public sealed class CreatePretradeChecklist
{
    public record Request(string Name, PretradeChecklistType Type, int ChecklistModelId, int UserId = 0) : ICommand<Result<int>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Checklist name cannot be null.")
                .NotEmpty().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Checklist name cannot be empty.");
            RuleFor(x => x.Type)
                .Cascade(CascadeMode.Stop)
                .Must(type => Enum.IsDefined(type))
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Checklist type must be a valid PretradeChecklistType value.");
            RuleFor(x => x.ChecklistModelId)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0).WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Checklist model id must be greater than 0.");
        }
    }

    public sealed class Handler(ITradeDbContext context) : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            if (request.UserId == 0)
            {
                return Result<int>.Failure(Error.Create("Unauthorized."));
            }

            bool modelExists = await context.ChecklistModels
                .AnyAsync(model => model.Id == request.ChecklistModelId && model.CreatedBy == request.UserId, cancellationToken);

            if (!modelExists)
            {
                return Result<int>.Failure(Error.Create($"Checklist model with id {request.ChecklistModelId} not found."));
            }

            PretradeChecklist checklist = new()
            {
                Id = 0,
                Name = request.Name,
                CheckListType = request.Type,
                ChecklistModelId = request.ChecklistModelId,
            };

            await context.PretradeChecklists.AddAsync(checklist, cancellationToken);

            int insertedRow = await context.SaveChangesAsync(cancellationToken);

            return insertedRow > 0 ? Result<int>.Success(checklist.Id)
                : Result<int>.Failure(Error.Create("Failed to create Pretrade Checklist."));
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.PretradeChecklists);

            group.MapPost("/", async ([FromBody] Request request, ClaimsPrincipal user, ISender sender) => {
                Result<int> result = await sender.Send(request with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Created($"/api/v1/pretrade-checklists/{result.Value}", result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<int>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Create a new pretrade checklist.")
            .WithDescription("Creates a new pretrade checklist with the given details.")
            .WithTags(Tags.PretradeChecklists)
            .RequireAuthorization();
        }
    }
}
