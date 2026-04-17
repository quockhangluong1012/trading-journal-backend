namespace TradingJournal.Modules.Trades.Features.V1.ChecklistModels;

public sealed class GetChecklistModelDetail
{
    public record Request(int Id, int UserId = 0) : ICommand<Result<ChecklistModelDetailViewModel>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Id must be greater than 0");
        }
    }

    public sealed class Handler(ITradeDbContext context) : ICommandHandler<Request, Result<ChecklistModelDetailViewModel>>
    {
        public async Task<Result<ChecklistModelDetailViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            ChecklistModel? model = await context.ChecklistModels
                .AsNoTracking()
                .Include(m => m.Criteria)
                .FirstOrDefaultAsync(m => m.Id == request.Id && m.CreatedBy == request.UserId, cancellationToken);

            if (model is null)
                return Result<ChecklistModelDetailViewModel>.Failure(
                    Error.Create($"Checklist model with id {request.Id} not found."));

            ChecklistModelDetailViewModel viewModel = new(
                model.Id,
                model.Name,
                model.Description,
                [.. model.Criteria.Select(c => new PretradeChecklistViewModel(
                    c.Id,
                    c.Name,
                    c.CheckListType,
                    c.ChecklistModelId,
                    model.Name))]);

            return Result<ChecklistModelDetailViewModel>.Success(viewModel);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.ChecklistModels);

            group.MapGet("/{id:int}", async (int id, ISender sender) =>
            {
                Result<ChecklistModelDetailViewModel> result = await sender.Send(new Request(id));
                return result.IsSuccess ? Results.Ok(result)
                    : Results.NotFound(result);
            })
            .Produces<Result<ChecklistModelDetailViewModel>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Get checklist model detail with all criteria.")
            .WithTags(Tags.ChecklistModels)
            .RequireAuthorization();
        }
    }
}
