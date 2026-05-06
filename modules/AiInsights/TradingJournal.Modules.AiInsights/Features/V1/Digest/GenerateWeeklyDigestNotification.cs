using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Messaging.Shared.Contracts;
using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Modules.AiInsights.Features.V1.Digest;

public sealed class GenerateWeeklyDigestNotification
{
    public sealed record Request(int UserId = 0) : ICommand<Result<AiWeeklyDigestResultDto>>;

    public sealed class Handler(
        IOpenRouterAIService aiService,
        IEventBus eventBus)
        : ICommandHandler<Request, Result<AiWeeklyDigestResultDto>>
    {
        public async Task<Result<AiWeeklyDigestResultDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            try
            {
                AiWeeklyDigestResultDto? response = await aiService.GenerateWeeklyDigestAsync(
                    new AiWeeklyDigestRequestDto(DateTime.UtcNow, request.UserId),
                    cancellationToken);

                if (response is null)
                {
                    return Result<AiWeeklyDigestResultDto>.Failure(Error.Create("AI weekly digest returned empty response."));
                }

                await eventBus.PublishAsync(new AiWeeklyDigestGeneratedEvent(
                    Guid.NewGuid(),
                    request.UserId,
                    response.Headline,
                    response.Summary,
                    response.FocusForNextWeek,
                    response.KeyWins,
                    response.KeyRisks,
                    response.ActionItems), cancellationToken);

                return Result<AiWeeklyDigestResultDto>.Success(response);
            }
            catch (InvalidOperationException ex)
            {
                return Result<AiWeeklyDigestResultDto>.Failure(
                    Error.Create(ex.InnerException != null ? ex.InnerException.Message : ex.Message));
            }
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.AiDigest);

            group.MapPost("/notify-weekly", async (ISender sender, ClaimsPrincipal user) =>
            {
                Result<AiWeeklyDigestResultDto> result = await sender.Send(new Request(user.GetCurrentUserId()));

                return ToHttpResult(result);
            })
            .Produces<Result<AiWeeklyDigestResultDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Generate and push the weekly AI digest notification.")
            .WithDescription("Builds a weekly AI digest for the current user and sends it through the notification system.")
            .WithTags(Tags.AiDigest)
            .RequireAuthorization();
        }

        private static IResult ToHttpResult(Result<AiWeeklyDigestResultDto> result)
        {
            if (result.IsSuccess)
            {
                return Results.Ok(result);
            }

            return result.Errors.Any(error => error.Code == HttpStatusCode.BadRequest.ToString())
                ? Results.BadRequest(result)
                : Results.Problem(result.Errors[0].Description);
        }
    }
}