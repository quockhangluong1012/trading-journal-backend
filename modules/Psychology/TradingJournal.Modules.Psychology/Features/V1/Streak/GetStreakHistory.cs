using TradingJournal.Modules.Psychology.ViewModel;

namespace TradingJournal.Modules.Psychology.Features.V1.Streak;

public sealed class GetStreakHistory
{
    internal record Request(int UserId = 0, int Days = 30) : IQuery<Result<List<StreakViewModel>>>;

    internal sealed class Handler(IPsychologyDbContext context) : IQueryHandler<Request, Result<List<StreakViewModel>>>
    {
        public async Task<Result<List<StreakViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            DateTimeOffset since = DateTimeOffset.UtcNow.AddDays(-request.Days);

            var records = await context.StreakRecords
                .AsNoTracking()
                .Where(s => s.CreatedBy == request.UserId && s.RecordedAt >= since)
                .OrderBy(s => s.RecordedAt)
                .ToListAsync(cancellationToken);

            var result = records.Select(GetCurrentStreak.MapToViewModel).ToList();

            return Result<List<StreakViewModel>>.Success(result);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("api/v1/streaks/history", async (ClaimsPrincipal user, ISender sender, int? days) =>
            {
                var result = await sender.Send(new Request(user.GetCurrentUserId(), days ?? 30));
                return result;
            })
            .Produces<Result<List<StreakViewModel>>>(StatusCodes.Status200OK)
            .WithSummary("Get streak history")
            .WithDescription("Returns streak snapshots for the specified number of days (default 30).")
            .WithTags("Streak Tracking")
            .RequireAuthorization();
        }
    }
}
