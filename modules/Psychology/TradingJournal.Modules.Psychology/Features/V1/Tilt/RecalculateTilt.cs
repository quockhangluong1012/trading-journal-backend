using TradingJournal.Modules.Psychology.Services;
using TradingJournal.Modules.Psychology.ViewModel;

namespace TradingJournal.Modules.Psychology.Features.V1.Tilt;

public sealed class RecalculateTilt
{
    internal record Request(int UserId = 0) : ICommand<Result<TiltScoreViewModel>>;

    internal sealed class Handler(ITiltDetectionService tiltService) : ICommandHandler<Request, Result<TiltScoreViewModel>>
    {
        public async Task<Result<TiltScoreViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            var snapshot = await tiltService.RecalculateTiltAsync(request.UserId, cancellationToken);

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
            app.MapPost("api/v1/tilt/recalculate", async (ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request(user.GetCurrentUserId()));
                return result;
            })
            .Produces<Result<TiltScoreViewModel>>(StatusCodes.Status200OK)
            .WithSummary("Recalculate tilt score")
            .WithDescription("Forces a recalculation of the tilt score based on current trading data. May trigger circuit breaker notification.")
            .WithTags("Tilt Detection")
            .RequireAuthorization();
        }
    }
}
