namespace TradingJournal.Modules.Trades.Features.V1.PretradeChecklists;

public sealed class GetPretradeChecklists
{
    public record Request(int UserId = 0) : ICommand<Result<IReadOnlyCollection<PretradeChecklistViewModel>>>;

    public sealed class Handler(ITradeDbContext context) : ICommandHandler<Request, Result<IReadOnlyCollection<PretradeChecklistViewModel>>>
    {
        public async Task<Result<IReadOnlyCollection<PretradeChecklistViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<PretradeChecklistViewModel> checklistViewModels = await context.PretradeChecklists
                .AsNoTracking()
                .Where(checklist => checklist.ChecklistModel.CreatedBy == request.UserId)
                .Select(checklist => new PretradeChecklistViewModel(
                    checklist.Id,
                    checklist.Name,
                    checklist.CheckListType,
                    checklist.ChecklistModelId,
                    checklist.ChecklistModel.Name))
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyCollection<PretradeChecklistViewModel>>.Success(checklistViewModels);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.PretradeChecklists);

            group.MapGet("/", async (ISender sender) => {
                Result<IReadOnlyCollection<PretradeChecklistViewModel>> result = await sender.Send(new Request());
                return result.IsSuccess ? Results.Ok(result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<IReadOnlyCollection<PretradeChecklistViewModel>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Get all pretrade checklists.")
            .WithTags(Tags.PretradeChecklists)
            .RequireAuthorization();
        }
    }
}
