using TradingJournal.Modules.Psychology.Services;
using TradingJournal.Modules.Psychology.ViewModel;

namespace TradingJournal.Modules.Psychology.Features.V1.Streak;

public sealed class GetCurrentStreak
{
    internal record Request(int UserId = 0) : IQuery<Result<StreakViewModel>>;

    internal sealed class Handler(IStreakTrackingService streakService) : IQueryHandler<Request, Result<StreakViewModel>>
    {
        public async Task<Result<StreakViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            var record = await streakService.GetCurrentStreakAsync(request.UserId, cancellationToken);

            if (record is null)
            {
                return Result<StreakViewModel>.Success(new StreakViewModel
                {
                    StreakType = StreakType.None.ToString(),
                    Length = 0,
                    StreakPnl = 0,
                    BestWinStreak = 0,
                    WorstLossStreak = 0,
                    TotalClosedTrades = 0,
                    RecordedAt = DateTime.UtcNow
                });
            }

            return Result<StreakViewModel>.Success(MapToViewModel(record));
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("api/v1/streaks/current", async (ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request(user.GetCurrentUserId()));
                return result;
            })
            .Produces<Result<StreakViewModel>>(StatusCodes.Status200OK)
            .WithSummary("Get current streak")
            .WithDescription("Returns the latest streak snapshot for the authenticated user.")
            .WithTags("Streak Tracking")
            .RequireAuthorization();
        }
    }

    internal static StreakViewModel MapToViewModel(StreakRecord record) => new()
    {
        StreakType = record.StreakType.ToString(),
        Length = record.Length,
        StreakPnl = record.StreakPnl,
        BestWinStreak = record.BestWinStreak,
        WorstLossStreak = record.WorstLossStreak,
        TotalClosedTrades = record.TotalClosedTrades,
        RecordedAt = record.RecordedAt
    };
}
