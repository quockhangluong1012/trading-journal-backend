namespace TradingJournal.Modules.Trades.Features.V1.ChecklistModels;

public sealed class CreateChecklistModel
{
    internal record Request(string Name, string? Description, int UserId = 0) : ICommand<Result<int>>;

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Model name cannot be null.")
                .NotEmpty().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Model name cannot be empty.");
        }
    }

    internal sealed class Handler(ITradeDbContext context) : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            if (request.UserId == 0)
            {
                return Result<int>.Failure(Error.Create("Unauthorized."));
            }

            ChecklistModel model = new()
            {
                Id = 0,
                Name = request.Name,
                Description = request.Description,
                CreatedBy = request.UserId,
            };

            await context.ChecklistModels.AddAsync(model, cancellationToken);
            int insertedRow = await context.SaveChangesAsync(cancellationToken);

            return insertedRow > 0 ? Result<int>.Success(model.Id)
                : Result<int>.Failure(Error.Create("Failed to create checklist model."));
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.ChecklistModels);

            group.MapPost("/", async ([FromBody] Request request, ISender sender) =>
            {
                Result<int> result = await sender.Send(request);
                return result.IsSuccess ? Results.Created($"{ApiGroup.V1.ChecklistModels}/{result.Value}", result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<int>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Create a new checklist model.")
            .WithDescription("Creates a new checklist model with the given name and description.")
            .WithTags(Tags.ChecklistModels)
            .RequireAuthorization("AdminOnly");
        }
    }
}
