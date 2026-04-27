using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Modules.AiInsights.Features.V1.AiCoach;

public sealed class ChatWithCoach
{
    public sealed record Request(
        List<AiCoachMessageDto> Messages,
        int UserId = 0) : ICommand<Result<AiCoachResponseDto>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Messages)
                .NotEmpty()
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("At least one message is required.");

            RuleFor(x => x.Messages)
                .Must(messages => messages.Count <= 50)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Conversation history cannot exceed 50 messages.");
        }
    }

    public sealed class Handler(IOpenRouterAIService aiService) : ICommandHandler<Request, Result<AiCoachResponseDto>>
    {
        public async Task<Result<AiCoachResponseDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            try
            {
                AiCoachRequestDto aiRequest = new(request.Messages, request.UserId);

                AiCoachResponseDto response = await aiService.ChatWithCoachAsync(aiRequest, cancellationToken);

                return Result<AiCoachResponseDto>.Success(response);
            }
            catch (InvalidOperationException ex)
            {
                return Result<AiCoachResponseDto>.Failure(Error.Create(ex.InnerException != null ? ex.InnerException.Message : ex.Message));
            }
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.AiCoach);

            group.MapPost("/chat", async (ISender sender, [FromBody] Request request, ClaimsPrincipal user) =>
            {
                Result<AiCoachResponseDto> result = await sender.Send(request with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<AiCoachResponseDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Chat with AI Trading Coach.")
            .WithDescription("Send a conversational message to the AI Trading Coach. The coach has access to the trader's recent performance data and psychology entries.")
            .WithTags(Tags.AiCoach)
            .RequireAuthorization();
        }
    }
}
