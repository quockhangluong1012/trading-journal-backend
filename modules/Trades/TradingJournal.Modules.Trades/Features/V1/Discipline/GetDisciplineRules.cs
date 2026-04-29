using Mapster;

namespace TradingJournal.Modules.Trades.Features.V1.Discipline;

public sealed class GetDisciplineRules
{
    public sealed record Request(int UserId = 0) : IQuery<Result<List<DisciplineRuleViewModel>>>;

    public sealed class Handler(ITradeDbContext context)
        : IQueryHandler<Request, Result<List<DisciplineRuleViewModel>>>
    {
        public async Task<Result<List<DisciplineRuleViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            List<DisciplineRuleViewModel> rules = await context.DisciplineRules
                .AsNoTracking()
                .Where(r => r.CreatedBy == request.UserId)
                .OrderBy(r => r.SortOrder)
                .ThenBy(r => r.CreatedDate)
                .Select(r => new DisciplineRuleViewModel
                {
                    Id = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    Category = r.Category,
                    IsActive = r.IsActive,
                    SortOrder = r.SortOrder,
                    CreatedDate = r.CreatedDate
                })
                .ToListAsync(cancellationToken);

            return Result<List<DisciplineRuleViewModel>>.Success(rules);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Discipline);

            group.MapGet("/rules", async (ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request() with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<List<DisciplineRuleViewModel>>>(StatusCodes.Status200OK)
            .WithSummary("Get all discipline rules.")
            .WithTags(Tags.Discipline)
            .RequireAuthorization();
        }
    }
}
