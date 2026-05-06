namespace TradingJournal.Modules.Trades.Features.V1.Discipline;

public sealed class DeleteDisciplineRule
{
    public sealed record Request(int Id, int UserId = 0) : ICommand<Result>;

    public sealed class Handler(ITradeDbContext context, ICacheRepository cacheRepository) : ICommandHandler<Request, Result>
    {
        public async Task<Result> Handle(Request request, CancellationToken cancellationToken)
        {
            DisciplineRule? rule = await context.DisciplineRules
                .FirstOrDefaultAsync(r => r.Id == request.Id && r.CreatedBy == request.UserId, cancellationToken);

            if (rule is null)
                return Result.Failure(Error.Create("Rule not found."));

            rule.IsDisabled = true;
            await context.SaveChangesAsync(cancellationToken);
            await cacheRepository.RemoveCache(CacheKeys.DisciplineRulesForUser(request.UserId), cancellationToken);
            return Result.Success();
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Discipline);

            group.MapDelete("/rules/{id:int}", async (int id, ClaimsPrincipal user, ISender sender) =>
            {
                Result result = await sender.Send(new Request(id) with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess ? Results.NoContent() : Results.NotFound(result);
            })
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Delete a discipline rule.")
            .WithTags(Tags.Discipline)
            .RequireAuthorization();
        }
    }
}
