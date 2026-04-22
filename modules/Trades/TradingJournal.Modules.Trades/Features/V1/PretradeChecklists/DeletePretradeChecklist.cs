namespace TradingJournal.Modules.Trades.Features.V1.PretradeChecklists;

public sealed class DeletePretradeChecklist
{
    public record Request(int Id, int UserId = 0) : ICommand<Result>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Id)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0).WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Pretrade Checklist Id must be greater than 0.");
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

            bool isUsedInTradeHistory = await context.TradeHistoryChecklist
                .AsNoTracking()
                .AnyAsync(link => link.PretradeChecklistId == request.Id, cancellationToken);

            if (isUsedInTradeHistory)
            {
                return Result.Failure(Error.Create("Pretrade Checklist is already used in trade history and cannot be deleted."));
            }

            context.PretradeChecklists.Remove(checklist);

            int affectedRows = await context.SaveChangesAsync(cancellationToken);

            return affectedRows > 0 ? Result.Success()
                : Result.Failure(Error.Create("Failed to delete Pretrade Checklist."));
        }
    }
    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.PretradeChecklists);

            group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal user, ISender sender) => {
                Result result = await sender.Send(new Request(id) with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess ? Results.NoContent()
                    : Results.BadRequest(result);
            })
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Delete a pretrade checklist by its Id.")
            .WithTags(Tags.PretradeChecklists)
            .RequireAuthorization();
        }
    }
}
