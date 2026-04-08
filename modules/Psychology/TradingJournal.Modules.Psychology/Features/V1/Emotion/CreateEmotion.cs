using TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Modules.Psychology.Features.V1.Emotion;

public sealed class CreateEmotion
{
    public record Request(string Name, EmotionType EmotionType) : ICommand<Result<int>>;

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() 
        { 
            RuleFor(x => x.Name)
                .NotEmpty()
                .MaximumLength(100)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Name is required");

            RuleFor(x => x.EmotionType)
                .IsInEnum()
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("EmotionType is required");
        }
    }

    internal sealed class Handler(IPsychologyDbContext context, ICacheRepository cacheRepository) : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            bool exists = await context.EmotionTags.AsNoTracking()
                .AnyAsync(x => x.Name == request.Name && x.EmotionType == request.EmotionType, cancellationToken);
            
            if (exists)
            {
                return Result<int>.Failure(Error.Create("Emotion tag already exists."));
            }

            EmotionTag emotionTag = new()
            {
                Id = 0,
                Name = request.Name,
                EmotionType = request.EmotionType
            };

            await context.EmotionTags.AddAsync(emotionTag, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            await cacheRepository.RemoveCache(CacheKeys.EmotionTags, cancellationToken);

            return Result<int>.Success(emotionTag.Id);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/emotions");

            group.MapPost("/", async ([FromBody] Request request, ISender sender) =>
            {
                Result<int> result = await sender.Send(request);
                return result;
            })
            .Produces<Result<int>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Create a new emotion tag.")
            .WithDescription("Creates a new emotion tag/")
            .WithTags(Tags.Emotions)
            .RequireAuthorization();
        }
    }
}
