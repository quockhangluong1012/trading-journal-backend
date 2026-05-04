using TradingJournal.Modules.Psychology.Services;
using TradingJournal.Modules.Psychology.ViewModel;

namespace TradingJournal.Modules.Psychology.Features.V1.Karma;

public sealed class GetAchievements
{
    internal record Request(int UserId = 0) : IQuery<Result<List<AchievementViewModel>>>;

    internal sealed class Handler(IKarmaService karmaService) : IQueryHandler<Request, Result<List<AchievementViewModel>>>
    {
        public async Task<Result<List<AchievementViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            var achievements = await karmaService.GetAchievementsAsync(request.UserId, cancellationToken);
            return Result<List<AchievementViewModel>>.Success(achievements);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/karma");

            group.MapGet("achievements", async (ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request(user.GetCurrentUserId()));
                return result;
            })
            .Produces<Result<List<AchievementViewModel>>>(StatusCodes.Status200OK)
            .WithSummary("Get achievements")
            .WithDescription("Returns all achievements with their lock/unlock status for the authenticated user.")
            .WithTags("Karma")
            .RequireAuthorization();
        }
    }
}
