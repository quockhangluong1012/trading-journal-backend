using TradingJournal.Modules.Psychology.ViewModel;

namespace TradingJournal.Modules.Psychology.Features.V1.Tilt;

public sealed class GetTiltHistory
{
    internal record Request(int UserId = 0, int Days = 30) : IQuery<Result<List<TiltScoreViewModel>>>;

    internal sealed class Handler(IPsychologyDbContext context) : IQueryHandler<Request, Result<List<TiltScoreViewModel>>>
    {
        public async Task<Result<List<TiltScoreViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            DateTime since = DateTime.UtcNow.AddDays(-request.Days);

            var snapshots = await context.TiltSnapshots
                .AsNoTracking()
                .Where(t => t.CreatedBy == request.UserId && t.RecordedAt >= since)
                .OrderBy(t => t.RecordedAt)
                .ToListAsync(cancellationToken);

            var result = snapshots.Select(s => new TiltScoreViewModel
            {
                Score = s.Score,
                Level = s.Level.ToString(),
                ConsecutiveLosses = s.ConsecutiveLosses,
                ConsecutiveWins = s.ConsecutiveWins,
                TradesLastHour = s.TradesLastHour,
                RuleBreaksToday = s.RuleBreaksToday,
                TodayPnl = s.TodayPnl,
                CircuitBreakerTriggered = s.CircuitBreakerTriggered,
                CooldownUntil = s.CooldownUntil,
                RecordedAt = s.RecordedAt
            }).ToList();

            return Result<List<TiltScoreViewModel>>.Success(result);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("api/v1/tilt/history", async (ClaimsPrincipal user, ISender sender, int? days) =>
            {
                var result = await sender.Send(new Request(user.GetCurrentUserId(), days ?? 30));
                return result;
            })
            .Produces<Result<List<TiltScoreViewModel>>>(StatusCodes.Status200OK)
            .WithSummary("Get tilt score history")
            .WithDescription("Returns tilt score snapshots for the specified number of days (default 30).")
            .WithTags("Tilt Detection")
            .RequireAuthorization();
        }
    }
}
