namespace TradingJournal.Modules.Psychology.Features.V1.Psychology;

public sealed class CreateTodayPsychology
{
    public record Request(DateTimeOffset Date, string TodayTradingReview, List<int> EmotionTags, int UserId = 0,
        OverallMood OverallMood = OverallMood.Neutral,
        ConfidentLevel ConfidentLevel = ConfidentLevel.None) : ICommand<Result<int>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.ConfidentLevel)
                .IsInEnum().WithMessage("Invalid confident level.");
        }
    }

    internal sealed class Handler(IPsychologyDbContext context) : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle([FromBody] Request request, CancellationToken cancellationToken)
        {
            PsychologyJournal todayPsychology = new()
            {
                Id = 0,
                Date = request.Date,
                TodayTradingReview = request.TodayTradingReview,
                OverallMood = request.OverallMood,
                ConfidentLevel = request.ConfidentLevel,
                CreatedBy = request.UserId,
                PsychologyJournalEmotions = [.. request.EmotionTags.Select(e => new PsychologyJournalEmotion
                { 
                    Id = 0,
                    EmotionTagId = e
                })]
            };
            await context.PsychologyJournals.AddAsync(todayPsychology, cancellationToken);

            int affectedRow = await context.SaveChangesAsync(cancellationToken);

            return affectedRow > 0 ? Result<int>.Success(todayPsychology.Id) 
                : Result<int>.Failure(Error.Create("Failed to create today's psychology journal."));
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/psychology-journals");

            group.MapPost("/", async (Request request, ClaimsPrincipal user, ISender sender) =>
            {
                Result<int> result = await sender.Send(request with { UserId = user.GetCurrentUserId() });
                return result;
            })
            .Produces<Result<int>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Create a new psychology journal entry.")
            .WithDescription("Creates a new psychology journal entry for today.")
            .WithTags(Tags.PsychologyJournal)
            .RequireAuthorization();
        }
    }
}
