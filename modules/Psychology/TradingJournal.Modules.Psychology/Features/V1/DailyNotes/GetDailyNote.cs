using TradingJournal.Modules.Psychology.ViewModel;

namespace TradingJournal.Modules.Psychology.Features.V1.DailyNotes;

public sealed class GetDailyNote
{
    internal record Request(int UserId, DateOnly Date) : IQuery<Result<DailyNoteViewModel?>>;

    internal sealed class Handler(IPsychologyDbContext db) : IQueryHandler<Request, Result<DailyNoteViewModel?>>
    {
        public async Task<Result<DailyNoteViewModel?>> Handle(Request request, CancellationToken cancellationToken)
        {
            DateOnly dateOnly = request.Date;

            DailyNote? note = await db.DailyNotes
                .AsNoTracking()
                .Where(n => n.CreatedBy == request.UserId && n.NoteDate == dateOnly)
                .FirstOrDefaultAsync(cancellationToken);

            if (note is null)
            {
                return Result<DailyNoteViewModel?>.Success(null);
            }

            return Result<DailyNoteViewModel?>.Success(new DailyNoteViewModel
            {
                Id = note.Id,
                NoteDate = note.NoteDate,
                DailyBias = note.DailyBias,
                MarketStructureNotes = note.MarketStructureNotes,
                KeyLevelsAndLiquidity = note.KeyLevelsAndLiquidity,
                NewsAndEvents = note.NewsAndEvents,
                SessionFocus = note.SessionFocus,
                RiskAppetite = note.RiskAppetite,
                MentalState = note.MentalState,
                KeyRulesAndReminders = note.KeyRulesAndReminders,
                CreatedDate = note.CreatedDate,
                UpdatedDate = note.UpdatedDate,
            });
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("api/v1/daily-notes/{date}", async (DateOnly date, ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request(user.GetCurrentUserId(), date));
                return result;
            })
            .Produces<Result<DailyNoteViewModel?>>(StatusCodes.Status200OK)
            .WithSummary("Get daily note")
            .WithDescription("Returns the daily trading note for the given date and authenticated user.")
            .WithTags("Daily Notes")
            .RequireAuthorization();
        }
    }
}
