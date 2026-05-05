using TradingJournal.Modules.Psychology.ViewModel;

namespace TradingJournal.Modules.Psychology.Features.V1.DailyNotes;

public sealed class UpsertDailyNote
{
    public record RequestBody
    {
        public DateOnly NoteDate { get; init; }
        public string DailyBias { get; init; } = string.Empty;
        public string MarketStructureNotes { get; init; } = string.Empty;
        public string KeyLevelsAndLiquidity { get; init; } = string.Empty;
        public string NewsAndEvents { get; init; } = string.Empty;
        public string SessionFocus { get; init; } = string.Empty;
        public string RiskAppetite { get; init; } = string.Empty;
        public string MentalState { get; init; } = string.Empty;
        public string KeyRulesAndReminders { get; init; } = string.Empty;
    }

    internal record Request(int UserId, RequestBody Body) : ICommand<Result<DailyNoteViewModel>>;

    internal sealed class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Body.DailyBias)
                .MaximumLength(16)
                .Must(v => string.IsNullOrEmpty(v) || v is "Bullish" or "Bearish" or "Neutral")
                .WithMessage("DailyBias must be Bullish, Bearish, or Neutral.");

            RuleFor(x => x.Body.RiskAppetite)
                .MaximumLength(16)
                .Must(v => string.IsNullOrEmpty(v) || v is "Conservative" or "Normal" or "Aggressive")
                .WithMessage("RiskAppetite must be Conservative, Normal, or Aggressive.");

            RuleFor(x => x.Body.SessionFocus)
                .MaximumLength(128);
        }
    }

    internal sealed class Handler(IPsychologyDbContext db) : ICommandHandler<Request, Result<DailyNoteViewModel>>
    {
        public async Task<Result<DailyNoteViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            DateOnly dateOnly = request.Body.NoteDate;

            DailyNote? existing = await db.DailyNotes
                .Where(n => n.CreatedBy == request.UserId && n.NoteDate == dateOnly)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is not null)
            {
                existing.DailyBias = request.Body.DailyBias;
                existing.MarketStructureNotes = request.Body.MarketStructureNotes;
                existing.KeyLevelsAndLiquidity = request.Body.KeyLevelsAndLiquidity;
                existing.NewsAndEvents = request.Body.NewsAndEvents;
                existing.SessionFocus = request.Body.SessionFocus;
                existing.RiskAppetite = request.Body.RiskAppetite;
                existing.MentalState = request.Body.MentalState;
                existing.KeyRulesAndReminders = request.Body.KeyRulesAndReminders;
            }
            else
            {
                existing = new DailyNote
                {
                    NoteDate = dateOnly,
                    DailyBias = request.Body.DailyBias,
                    MarketStructureNotes = request.Body.MarketStructureNotes,
                    KeyLevelsAndLiquidity = request.Body.KeyLevelsAndLiquidity,
                    NewsAndEvents = request.Body.NewsAndEvents,
                    SessionFocus = request.Body.SessionFocus,
                    RiskAppetite = request.Body.RiskAppetite,
                    MentalState = request.Body.MentalState,
                    KeyRulesAndReminders = request.Body.KeyRulesAndReminders,
                };

                db.DailyNotes.Add(existing);
            }

            await db.SaveChangesAsync(cancellationToken);

            return Result<DailyNoteViewModel>.Success(new DailyNoteViewModel
            {
                Id = existing.Id,
                NoteDate = existing.NoteDate,
                DailyBias = existing.DailyBias,
                MarketStructureNotes = existing.MarketStructureNotes,
                KeyLevelsAndLiquidity = existing.KeyLevelsAndLiquidity,
                NewsAndEvents = existing.NewsAndEvents,
                SessionFocus = existing.SessionFocus,
                RiskAppetite = existing.RiskAppetite,
                MentalState = existing.MentalState,
                KeyRulesAndReminders = existing.KeyRulesAndReminders,
                CreatedDate = existing.CreatedDate,
                UpdatedDate = existing.UpdatedDate,
            });
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPut("api/v1/daily-notes", async ([FromBody] RequestBody body, ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request(user.GetCurrentUserId(), body));
                return result;
            })
            .Produces<Result<DailyNoteViewModel>>(StatusCodes.Status200OK)
            .WithSummary("Create or update daily note")
            .WithDescription("Creates a new daily note or updates an existing one for the given date.")
            .WithTags("Daily Notes")
            .RequireAuthorization();
        }
    }
}
