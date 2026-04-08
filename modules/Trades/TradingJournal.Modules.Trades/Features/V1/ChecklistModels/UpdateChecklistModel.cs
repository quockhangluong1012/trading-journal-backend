namespace TradingJournal.Modules.Trades.Features.V1.ChecklistModels;

public sealed class UpdateChecklistModel
{
    internal record Request(int Id, string Name, string? Description, int UserId = 0) : ICommand<Result<bool>>;

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0).WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Model id must be greater than 0.");

            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Model name cannot be null.")
                .NotEmpty().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Model name cannot be empty.");
        }
    }

    internal sealed class Handler(ITradeDbContext context) : ICommandHandler<Request, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Request request, CancellationToken cancellationToken)
        {
            ChecklistModel? model = await context.ChecklistModels
                .FirstOrDefaultAsync(m => m.Id == request.Id && m.CreatedBy == request.UserId, cancellationToken);

            if (model is null)
                return Result<bool>.Failure(Error.Create($"Checklist model with id {request.Id} not found."));

            model.Name = request.Name;
            model.Description = request.Description;

            int updatedRows = await context.SaveChangesAsync(cancellationToken);

            return updatedRows > 0 ? Result<bool>.Success(true)
                : Result<bool>.Failure(Error.Create("Failed to update checklist model."));
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.ChecklistModels);

            group.MapPut("/", async ([FromBody] Request request, ISender sender) =>
            {
                Result<bool> result = await sender.Send(request);
                return result.IsSuccess ? Results.Ok(result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<bool>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Update an existing checklist model.")
            .WithTags(Tags.ChecklistModels)
            .RequireAuthorization();
        }
    }
}
