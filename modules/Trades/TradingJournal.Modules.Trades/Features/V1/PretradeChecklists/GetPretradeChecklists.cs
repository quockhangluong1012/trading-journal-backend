namespace TradingJournal.Modules.Trades.Features.V1.PretradeChecklists;

public sealed class GetPretradeChecklists
{
    internal record Request() : ICommand<Result<IReadOnlyCollection<PretradeChecklistViewModel>>>;

    internal sealed class Handler(ITradeDbContext context) : ICommandHandler<Request, Result<IReadOnlyCollection<PretradeChecklistViewModel>>>
    {
        public async Task<Result<IReadOnlyCollection<PretradeChecklistViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            List<PretradeChecklist> checklists = await context.PretradeChecklists
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            IReadOnlyCollection<PretradeChecklistViewModel> checklistViewModels = [.. checklists.Select(checklist => new PretradeChecklistViewModel(Id: checklist.Id,
                Name: checklist.Name,
                CheckListType: checklist.CheckListType))];

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
