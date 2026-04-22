namespace TradingJournal.Modules.Psychology.Features.V1.Emotion;

public sealed class DeleteEmotion
{
    public sealed record Request(int Id) : ICommand<Result<bool>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator() 
        { 
            RuleFor(x => x.Id)
                .NotEmpty()
                .GreaterThan(0)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Id is required");
        }
    }

    public sealed class Handler(IPsychologyDbContext context, ICacheRepository cacheRepository) : ICommandHandler<Request, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Request request, CancellationToken cancellationToken)
        {
            EmotionTag? emotionTag = await context.EmotionTags.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            
            if (emotionTag is null)
            {
                return Result<bool>.Failure(Error.Create("Emotion tag not found."));
            }

            context.EmotionTags.Remove(emotionTag);

            await context.SaveChangesAsync(cancellationToken);

            await cacheRepository.RemoveCache(CacheKeys.EmotionTags, cancellationToken);

            return Result<bool>.Success(true);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/emotions");

            group.MapDelete("{id}", async (int id, ISender sender) =>
            {
                Result<bool> result = await sender.Send(new Request(id));
                return result;
            })
            .Produces<Result<bool>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Delete an emotion tag.")
            .WithDescription("Deletes an emotion tag/")
            .WithTags(Tags.Emotions)
            .RequireAuthorization("AdminOnly");
        }
    }
}
