namespace TradingJournal.Modules.Psychology.Features.V1.Psychology;

public sealed class UpdatePsychologyJournal
{
    public record Request(int Id, DateTime Date, string TodayTradingReview, List<int> EmotionTags, int UserId = 0,
        OverallMood OverallMood = OverallMood.Neutral,
        ConfidentLevel ConfidentLevel = ConfidentLevel.None) : ICommand<Result<bool>>;

    internal sealed class Handler(IPsychologyDbContext context) : ICommandHandler<Request, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Request request, CancellationToken cancellationToken)
        {
            PsychologyJournal? psychologyJournal = await context.PsychologyJournals.FirstOrDefaultAsync(x => x.Id == request.Id && x.CreatedBy == request.UserId, cancellationToken);
            
            if (psychologyJournal is null)
            {
                return Result<bool>.Failure(Error.Create("Psychology journal not found."));
            }

            context.PsychologyJournalEmotions.RemoveRange(psychologyJournal.PsychologyJournalEmotions);

            psychologyJournal.Date = request.Date;
            psychologyJournal.TodayTradingReview = request.TodayTradingReview;
            psychologyJournal.OverallMood = request.OverallMood;
            psychologyJournal.ConfidentLevel = request.ConfidentLevel;
            psychologyJournal.PsychologyJournalEmotions = [.. request.EmotionTags.Select(e => new PsychologyJournalEmotion
            { 
                Id = 0,
                EmotionTagId = e
            })];

            context.PsychologyJournals.Update(psychologyJournal);

            int affectedRow = await context.SaveChangesAsync(cancellationToken);

            return affectedRow > 0 ? Result<bool>.Success(true) 
                : Result<bool>.Failure(Error.Create("Failed to update today's psychology journal."));
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/psychology-journals");

            group.MapPut("/", async ([FromBody] Request request, ClaimsPrincipal user, ISender sender) =>
            {
                Result<bool> result = await sender.Send(request with { UserId = user.GetCurrentUserId() });
                return result;
            })  
            .Produces<Result<bool>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Update a psychology journal entry.")
            .WithDescription("Updates a psychology journal entry for today.")
            .WithTags(Tags.PsychologyJournal)
            .RequireAuthorization();
        }
    }
}
