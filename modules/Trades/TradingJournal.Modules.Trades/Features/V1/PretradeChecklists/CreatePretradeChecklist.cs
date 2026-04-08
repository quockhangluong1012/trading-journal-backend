namespace TradingJournal.Modules.Trades.Features.V1.PretradeChecklists;

public sealed class CreatePretradeChecklist
{
    internal record Request(string Name, PretradeChecklistType Type, int UserId = 0) : ICommand<Result<int>>;

    internal sealed class Validator : AbstractValidator<Request>
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

            PretradeChecklist checklist = new()
            {
                Id = 0,
                Name = request.Name,
                CheckListType = request.Type,
                CreatedBy = request.UserId,
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

            group.MapPost("/", async ([FromBody] Request request, ISender sender) => {
                Result<int> result = await sender.Send(request);

                return result.IsSuccess ? Results.Created($"/api/v1/pretrade-checklists/{result.Value}", result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<int>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Create a new pretrade checklist.")
            .WithDescription("Creates a new pretrade checklist with the given details.")
            .WithTags(Tags.PretradeChecklists)
            .RequireAuthorization("AdminOnly");
        }
    }
}
