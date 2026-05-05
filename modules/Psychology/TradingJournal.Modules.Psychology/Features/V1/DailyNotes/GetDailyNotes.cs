using TradingJournal.Modules.Psychology.ViewModel;

namespace TradingJournal.Modules.Psychology.Features.V1.DailyNotes;

/// <summary>
/// Lists daily notes for the authenticated user within an optional date range.
/// Supports weekly / monthly review by filtering on StartDate..EndDate.
/// </summary>
public sealed class GetDailyNotes
{
    public record Request(
        int UserId,
        DateOnly? StartDate = null,
        DateOnly? EndDate = null
    ) : IQuery<Result<List<DailyNoteSummaryViewModel>>>;

    internal sealed class Handler(IPsychologyDbContext db)
        : IQueryHandler<Request, Result<List<DailyNoteSummaryViewModel>>>
    {
        public async Task<Result<List<DailyNoteSummaryViewModel>>> Handle(
            Request request, CancellationToken cancellationToken)
        {
            IQueryable<DailyNote> query = db.DailyNotes
                .AsNoTracking()
                .Where(n => n.CreatedBy == request.UserId);

            if (request.StartDate.HasValue)
            {
                query = query.Where(n => n.NoteDate >= request.StartDate.Value);
            }

            if (request.EndDate.HasValue)
            {
                query = query.Where(n => n.NoteDate <= request.EndDate.Value);
            }

            List<DailyNote> notes = await query
                .OrderByDescending(n => n.NoteDate)
                .ToListAsync(cancellationToken);

            var viewModels = notes.Select(n => new DailyNoteSummaryViewModel
            {
                Id = n.Id,
                NoteDate = n.NoteDate,
                DailyBias = n.DailyBias,
                SessionFocus = n.SessionFocus,
                RiskAppetite = n.RiskAppetite,
                MentalState = n.MentalState,
                FilledFieldsCount = CountFilledFields(n),
                CreatedDate = n.CreatedDate,
                UpdatedDate = n.UpdatedDate,
            }).ToList();

            return Result<List<DailyNoteSummaryViewModel>>.Success(viewModels);
        }

        private static int CountFilledFields(DailyNote n)
        {
            int count = 0;
            if (!string.IsNullOrWhiteSpace(n.DailyBias)) count++;
            if (!string.IsNullOrWhiteSpace(n.MarketStructureNotes)) count++;
            if (!string.IsNullOrWhiteSpace(n.KeyLevelsAndLiquidity)) count++;
            if (!string.IsNullOrWhiteSpace(n.NewsAndEvents)) count++;
            if (!string.IsNullOrWhiteSpace(n.SessionFocus)) count++;
            if (!string.IsNullOrWhiteSpace(n.RiskAppetite)) count++;
            if (!string.IsNullOrWhiteSpace(n.MentalState)) count++;
            if (!string.IsNullOrWhiteSpace(n.KeyRulesAndReminders)) count++;
            return count;
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("api/v1/daily-notes", async (
                [FromQuery] DateOnly? startDate,
                [FromQuery] DateOnly? endDate,
                ClaimsPrincipal user,
                ISender sender) =>
            {
                var result = await sender.Send(
                    new Request(user.GetCurrentUserId(), startDate, endDate));
                return result;
            })
            .Produces<Result<List<DailyNoteSummaryViewModel>>>(StatusCodes.Status200OK)
            .WithSummary("List daily notes")
            .WithDescription(
                "Returns daily trading notes for the authenticated user. " +
                "Optionally filter by startDate and endDate for weekly/monthly review.")
            .WithTags("Daily Notes")
            .RequireAuthorization();
        }
    }
}
