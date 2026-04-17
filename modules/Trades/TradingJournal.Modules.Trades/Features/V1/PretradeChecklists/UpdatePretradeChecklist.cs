namespace TradingJournal.Modules.Trades.Features.V1.PretradeChecklists;

public sealed class UpdatePretradeChecklist
{
    public record Request(int Id, string Name, PretradeChecklistType Type, int ChecklistModelId, int UserId = 0) : ICommand<Result>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Id)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0).WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Pretrade Checklist Id must be greater than 0.");
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

    public sealed class Handler(ITradeDbContext context) : ICommandHandler<Request, Result>
    {
        public async Task<Result> Handle(Request request, CancellationToken cancellationToken)
        {
            PretradeChecklist? checklist = await context.PretradeChecklists
                .FirstOrDefaultAsync(c => c.Id == request.Id && c.ChecklistModel.CreatedBy == request.UserId, cancellationToken);

            if (checklist is null)
            {
                return Result.Failure(Error.Create("Pretrade Checklist not found."));
            }

            bool modelExists = await context.ChecklistModels
                .AnyAsync(model => model.Id == request.ChecklistModelId && model.CreatedBy == request.UserId, cancellationToken);

            if (!modelExists)
            {
                return Result.Failure(Error.Create($"Checklist model with id {request.ChecklistModelId} not found."));
            }

            checklist.Name = request.Name;
            checklist.CheckListType = request.Type;
            checklist.ChecklistModelId = request.ChecklistModelId;

            int affectedRows = await context.SaveChangesAsync(cancellationToken);

            return affectedRows > 0 ? Result.Success()
                : Result.Failure(Error.Create("Failed to update Pretrade Checklist."));
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.PretradeChecklists);

            group.MapPut("/", async ([FromBody] Request request, ISender sender) => {

                Result result = await sender.Send(request);
                return result.IsSuccess ? Results.NoContent()
                    : Results.BadRequest(result);
            })
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Update an existing pretrade checklist by its Id.")
            .WithTags(Tags.PretradeChecklists)
            .RequireAuthorization();
        }
    }
}
