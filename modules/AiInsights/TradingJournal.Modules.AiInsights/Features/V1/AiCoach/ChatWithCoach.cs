using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Domain;
using TradingJournal.Modules.AiInsights.Infrastructure;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Modules.AiInsights.Features.V1.AiCoach;

public sealed class ChatWithCoach
{
    public sealed record Request(
        List<AiCoachMessageDto> Messages,
        string? Mode = null,
        int UserId = 0) : ICommand<Result<AiCoachResponseDto>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        private const int MaxMessageCharacters = 4000;
        private const int MaxConversationCharacters = 20000;

        public Validator()
        {
            RuleFor(x => x.Messages)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("At least one message is required.")
                .NotEmpty()
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("At least one message is required.");

            RuleFor(x => x.Messages)
                .Must(messages => messages is not null && messages.Count <= 50)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Conversation history cannot exceed 50 messages.");

            RuleFor(x => x.Messages)
                .Must(messages => messages is not null && messages.All(message => message is not null))
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Conversation messages cannot contain null items.");

            RuleFor(x => x.Messages)
                .Must(messages => messages is not null && messages.Sum(message => message?.Content?.Length ?? 0) <= MaxConversationCharacters)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage($"Conversation content cannot exceed {MaxConversationCharacters} characters.");

            RuleForEach(x => x.Messages)
                .ChildRules(message =>
                {
                    message.RuleFor(m => m.Role)
                        .Must(AiCoachRoles.IsValid)
                        .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                        .WithMessage("Message role must be either 'user' or 'assistant'.");

                    message.RuleFor(m => m.Content)
                        .Must(content => !string.IsNullOrWhiteSpace(content))
                        .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                        .WithMessage("Message content is required.")
                        .MaximumLength(MaxMessageCharacters)
                        .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                        .WithMessage($"Message content cannot exceed {MaxMessageCharacters} characters.");
                });

            RuleFor(x => x.Mode)
                .Must(AiCoachModes.IsValid)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Mode must be either 'coach' or 'research'.");
        }
    }

    private static AiCoachRequestDto CreateAiRequest(Request request)
    {
        if (request.Messages.Any(static message => message is null))
        {
            throw new InvalidOperationException("Conversation messages cannot contain null items.");
        }

        return new AiCoachRequestDto(
            [.. request.Messages.Select(message =>
            {
                string role = AiCoachRoles.Normalize(message.Role);
                string content = string.Equals(role, AiCoachRoles.User, StringComparison.Ordinal)
                    ? OpenRouterAiService.SanitizePromptInput(message.Content)
                    : message.Content;

                return new AiCoachMessageDto(role, content);
            })],
            request.UserId,
            AiCoachModes.Normalize(request.Mode));
    }

    private static async Task TryPersistConversationSnapshotAsync(
        IAiInsightsDbContext context,
        AiCoachRequestDto aiRequest,
        AiCoachResponseDto response,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            List<AiCoachMessageDto> transcript =
            [
                .. aiRequest.Messages,
                new AiCoachMessageDto(AiCoachRoles.Assistant, response.Reply)
            ];

            await context.AiCoachConversations.AddAsync(new AiCoachConversation
            {
                Mode = aiRequest.Mode,
                MessageCount = transcript.Count,
                TranscriptJson = JsonSerializer.Serialize(transcript),
            }, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist AI coach conversation snapshot for user {UserId}.", aiRequest.UserId);
        }
    }

    private static string GetClientSafeErrorMessage(Exception exception)
    {
        string message = exception.InnerException?.Message ?? exception.Message;

        return message switch
        {
            "Conversation messages cannot contain null items." => message,
            _ when message.StartsWith("Unsupported AI coach mode", StringComparison.Ordinal) => "Unsupported AI coach mode.",
            _ => "AI coach is temporarily unavailable. Please try again."
        };
    }

    private static async Task WriteStreamEventAsync(
        HttpResponse response,
        object payload,
        CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(payload);
        await response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    public sealed class Handler(
        IOpenRouterAIService aiService,
        IAiInsightsDbContext context,
        ILogger<Handler>? logger = null) : ICommandHandler<Request, Result<AiCoachResponseDto>>
    {
        private readonly ILogger<Handler> _logger = logger ?? NullLogger<Handler>.Instance;

        public async Task<Result<AiCoachResponseDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            try
            {
                AiCoachRequestDto aiRequest = CreateAiRequest(request);

                AiCoachResponseDto response = await aiService.ChatWithCoachAsync(aiRequest, cancellationToken);

                await TryPersistConversationSnapshotAsync(context, aiRequest, response, _logger, cancellationToken);

                return Result<AiCoachResponseDto>.Success(response);
            }
            catch (InvalidOperationException ex)
            {
                return Result<AiCoachResponseDto>.Failure(Error.Create(GetClientSafeErrorMessage(ex)));
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
            .WithDescription("Send a conversational message to the AI coach. Use coach mode for personalized trade and psychology feedback, or research mode for ICT concept learning and deeper explanations.")
            .WithTags(Tags.AiCoach)
            .RequireRateLimiting("ai")
            .RequireAuthorization();

            group.MapPost("/chat/stream", async (
                HttpContext httpContext,
                IValidator<Request> validator,
                IOpenRouterAIService aiService,
                IAiInsightsDbContext context,
                ILogger<Endpoint> logger,
                [FromBody] Request request,
                ClaimsPrincipal user) =>
            {
                CancellationToken cancellationToken = httpContext.RequestAborted;
                Request authenticatedRequest = request with { UserId = user.GetCurrentUserId() };
                var validationResult = await validator.ValidateAsync(authenticatedRequest, cancellationToken);

                if (!validationResult.IsValid)
                {
                    string validationMessage = string.Join(" ", validationResult.Errors
                        .Select(error => error.ErrorMessage)
                        .Distinct(StringComparer.Ordinal));

                    httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await httpContext.Response.WriteAsJsonAsync(
                        Result<AiCoachResponseDto>.Failure(Error.Create(validationMessage)),
                        cancellationToken);
                    return;
                }

                AiCoachRequestDto aiRequest;

                try
                {
                    aiRequest = CreateAiRequest(authenticatedRequest);
                }
                catch (InvalidOperationException ex)
                {
                    httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await httpContext.Response.WriteAsJsonAsync(
                        Result<AiCoachResponseDto>.Failure(Error.Create(GetClientSafeErrorMessage(ex))),
                        cancellationToken);
                    return;
                }

                httpContext.Response.StatusCode = StatusCodes.Status200OK;
                httpContext.Response.ContentType = "text/event-stream";
                httpContext.Response.Headers.CacheControl = "no-cache";
                httpContext.Response.Headers.Append("X-Accel-Buffering", "no");

                StringBuilder replyBuilder = new();

                try
                {
                    await foreach (string chunk in aiService.StreamChatWithCoachAsync(aiRequest, cancellationToken))
                    {
                        if (string.IsNullOrEmpty(chunk))
                        {
                            continue;
                        }

                        replyBuilder.Append(chunk);
                        await WriteStreamEventAsync(httpContext.Response, new { type = "chunk", content = chunk }, cancellationToken);
                    }

                    await TryPersistConversationSnapshotAsync(
                        context,
                        aiRequest,
                        new AiCoachResponseDto(replyBuilder.ToString()),
                        logger,
                        cancellationToken);

                    await WriteStreamEventAsync(httpContext.Response, new { type = "done" }, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
                catch (InvalidOperationException ex)
                {
                    if (!httpContext.Response.HasStarted)
                    {
                        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await httpContext.Response.WriteAsJsonAsync(
                            Result<AiCoachResponseDto>.Failure(Error.Create(GetClientSafeErrorMessage(ex))),
                            cancellationToken);
                        return;
                    }

                    try
                    {
                        await WriteStreamEventAsync(
                            httpContext.Response,
                            new { type = "error", message = GetClientSafeErrorMessage(ex) },
                            cancellationToken);
                    }
                    catch (Exception writeException) when (writeException is not OperationCanceledException)
                    {
                        logger.LogDebug(writeException, "Failed to write AI coach stream error event.");
                    }
                }
            })
            .Accepts<Request>("application/json")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Stream AI Trading Coach responses.")
            .WithDescription("Streams AI coach replies as server-sent events for personalized coaching and research mode conversations.")
            .WithTags(Tags.AiCoach)
            .RequireRateLimiting("ai")
            .RequireAuthorization();
        }
    }
}
