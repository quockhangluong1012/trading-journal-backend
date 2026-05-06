using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Modules.AiInsights.Features.V1.Emotions;

public sealed class DetectEmotions
{
    public sealed record Request(
        string TextContent,
        int UserId = 0) : ICommand<Result<EmotionDetectionResultDto>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.TextContent)
                .NotEmpty()
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Text content is required for emotion detection.");

            RuleFor(x => x.TextContent)
                .MaximumLength(5000)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Text content cannot exceed 5000 characters.");
        }
    }

    public sealed class Handler(IOpenRouterAIService aiService) : ICommandHandler<Request, Result<EmotionDetectionResultDto>>
    {
        public async Task<Result<EmotionDetectionResultDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            try
            {
                EmotionDetectionRequestDto aiRequest = new(request.TextContent, request.UserId);

                EmotionDetectionResultDto? response = await aiService.DetectEmotionsAsync(aiRequest, cancellationToken);

                return response is not null
                    ? Result<EmotionDetectionResultDto>.Success(response)
                    : Result<EmotionDetectionResultDto>.Failure(Error.Create("AI emotion detection returned empty response."));
            }
            catch (InvalidOperationException ex)
            {
                return Result<EmotionDetectionResultDto>.Failure(
                    Error.Create(ex.InnerException != null ? ex.InnerException.Message : ex.Message));
            }
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.AiEmotions);

            group.MapPost("/detect", async (ISender sender, [FromBody] Request request, ClaimsPrincipal user) =>
            {
                Result<EmotionDetectionResultDto> result = await sender.Send(request with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<EmotionDetectionResultDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Detect emotions from text using AI.")
            .WithDescription("Analyzes trade notes, daily notes, or psychology entries to detect emotional state and suggest emotion tags.")
            .WithTags(Tags.AiEmotions)
            .RequireAuthorization();
        }
    }
}
