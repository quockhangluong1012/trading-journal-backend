using TradingJournal.Modules.Psychology.Services;
using TradingJournal.Modules.Psychology.ViewModel;

namespace TradingJournal.Modules.Psychology.Features.V1.Streak;

public sealed class RecalculateStreak
{
    internal record Request(int UserId = 0) : ICommand<Result<StreakViewModel>>;

    internal sealed class Handler(IStreakTrackingService streakService) : ICommandHandler<Request, Result<StreakViewModel>>
    {
        public async Task<Result<StreakViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            var record = await streakService.RecalculateStreakAsync(request.UserId, cancellationToken);

            return Result<StreakViewModel>.Success(GetCurrentStreak.MapToViewModel(record));
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("api/v1/streaks/recalculate", async (ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request(user.GetCurrentUserId()));
                return result;
            })
            .Produces<Result<StreakViewModel>>(StatusCodes.Status200OK)
            .WithSummary("Recalculate streak")
            .WithDescription("Forces a recalculation of the win/loss streak based on current trade data. May trigger streak alert notification.")
            .WithTags("Streak Tracking")
            .RequireAuthorization();
        }
    }
}
