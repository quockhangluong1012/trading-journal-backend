namespace TradingJournal.Modules.Trades.Features.V1.ChecklistModels;

public sealed class GetChecklistModels
{
    public record Request(int UserId = 0, int PageIndex = 1, int PageSize = 10) : ICommand<Result<IReadOnlyCollection<ChecklistModelViewModel>>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.PageIndex).GreaterThan(0).WithMessage("Page index must be greater than 0.");
            RuleFor(r => r.PageSize).GreaterThan(0).WithMessage("Page size must be greater than 0.");
        }
    }

    public sealed class Handler(ITradeDbContext context) : ICommandHandler<Request, Result<IReadOnlyCollection<ChecklistModelViewModel>>>
    {
        public async Task<Result<IReadOnlyCollection<ChecklistModelViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<ChecklistModelViewModel> models = await context.ChecklistModels
                .AsNoTracking()
                .Where(m => m.CreatedBy == request.UserId)
                .Select(m => new ChecklistModelViewModel(
                    m.Id,
                    m.Name,
                    m.Description,
                    m.Criteria.Count))
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyCollection<ChecklistModelViewModel>>.Success(models);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.ChecklistModels);

            group.MapGet("/", async (ClaimsPrincipal user, ISender sender) =>
            {
                Result<IReadOnlyCollection<ChecklistModelViewModel>> result = await sender.Send(new Request(user.GetCurrentUserId()));
                return result.IsSuccess ? Results.Ok(result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<IReadOnlyCollection<ChecklistModelViewModel>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get all checklist models.")
            .WithTags(Tags.ChecklistModels)
            .RequireAuthorization();
        }
    }
}
