namespace TradingJournal.Modules.Setups.Features.V1.TradingSetups;

public sealed class GetTradingSetups
{
    public record Request(int UserId = 0) : ICommand<Result<IReadOnlyCollection<TradingSetupViewModel>>>;

    internal static IQueryable<TradingSetupViewModel> BuildQuery(IQueryable<TradingSetup> tradingSetups, int userId)
    {
        return tradingSetups
            .Where(setup => setup.CreatedBy == userId && !setup.IsDisabled)
            .OrderByDescending(setup => setup.UpdatedDate ?? setup.CreatedDate)
            .Select(setup => new TradingSetupViewModel(
                setup.Id,
                setup.Name,
                setup.Description,
                setup.Steps.Count(step => step.NodeType != "start" && step.NodeType != "end"),
                setup.CreatedDate,
                setup.UpdatedDate ?? setup.CreatedDate));
    }

    public sealed class Handler(ISetupDbContext context, ICacheRepository cacheRepository) : ICommandHandler<Request, Result<IReadOnlyCollection<TradingSetupViewModel>>>
    {
        public async Task<Result<IReadOnlyCollection<TradingSetupViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            if (request.UserId <= 0)
            {
                return Result<IReadOnlyCollection<TradingSetupViewModel>>.Failure(Error.Create("Current user is required."));
            }

            List<TradingSetupViewModel> setups = await cacheRepository.GetOrCreateAsync<List<TradingSetupViewModel>>(
                CacheKeys.SetupsForUser(request.UserId),
                async ct => await BuildQuery(
                        context.TradingSetups.AsNoTracking(),
                        request.UserId)
                    .ToListAsync(ct),
                expiration: TimeSpan.FromMinutes(5),
                cancellationToken: cancellationToken) ?? [];

            return Result<IReadOnlyCollection<TradingSetupViewModel>>.Success(setups);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradingSetups);

            group.MapGet("/", async (ClaimsPrincipal user, ISender sender) =>
            {
                Result<IReadOnlyCollection<TradingSetupViewModel>> result = await sender.Send(new Request(user.GetCurrentUserId()));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<IReadOnlyCollection<TradingSetupViewModel>>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get the current user's trading setup flow charts.")
            .WithTags(Tags.TradingSetups)
            .RequireAuthorization();
        }
    }
}
