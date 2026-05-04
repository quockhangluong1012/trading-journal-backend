using TradingJournal.Modules.Psychology.Services;
using TradingJournal.Modules.Psychology.ViewModel;

namespace TradingJournal.Modules.Psychology.Features.V1.Tilt;

public sealed class GetTiltScore
{
    internal record Request(int UserId = 0) : IQuery<Result<TiltScoreViewModel>>;

    internal sealed class Handler(ITiltDetectionService tiltService) : IQueryHandler<Request, Result<TiltScoreViewModel>>
    {
        public async Task<Result<TiltScoreViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            var snapshot = await tiltService.GetCurrentTiltAsync(request.UserId, cancellationToken);

            if (snapshot is null)
            {
                return Result<TiltScoreViewModel>.Success(new TiltScoreViewModel
                {
                    Score = 0,
                    Level = TiltLevel.Calm.ToString(),
                    ConsecutiveLosses = 0,
                    ConsecutiveWins = 0,
                    TradesLastHour = 0,
                    RuleBreaksToday = 0,
                    TodayPnl = 0,
                    CircuitBreakerTriggered = false,
                    CooldownUntil = null,
                    RecordedAt = DateTimeOffset.UtcNow
                });
            }

            return Result<TiltScoreViewModel>.Success(new TiltScoreViewModel
            {
                Score = snapshot.Score,
                Level = snapshot.Level.ToString(),
                ConsecutiveLosses = snapshot.ConsecutiveLosses,
                ConsecutiveWins = snapshot.ConsecutiveWins,
                TradesLastHour = snapshot.TradesLastHour,
                RuleBreaksToday = snapshot.RuleBreaksToday,
                TodayPnl = snapshot.TodayPnl,
                CircuitBreakerTriggered = snapshot.CircuitBreakerTriggered,
                CooldownUntil = snapshot.CooldownUntil,
                RecordedAt = snapshot.RecordedAt
            });
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("api/v1/tilt/score", async (ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request(user.GetCurrentUserId()));
                return result;
            })
            .Produces<Result<TiltScoreViewModel>>(StatusCodes.Status200OK)
            .WithSummary("Get current tilt score")
            .WithDescription("Returns the latest tilt score snapshot for the authenticated user.")
            .WithTags("Tilt Detection")
            .RequireAuthorization();
        }
    }
}
