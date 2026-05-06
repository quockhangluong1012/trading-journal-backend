using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Modules.AiInsights.Features.V1.Briefing;

public sealed class GenerateMorningBriefing
{
    public sealed record Request(
        int UserId = 0) : ICommand<Result<MorningBriefingResultDto>>;

    public sealed class Handler(IOpenRouterAIService aiService) : ICommandHandler<Request, Result<MorningBriefingResultDto>>
    {
        public async Task<Result<MorningBriefingResultDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            try
            {
                MorningBriefingRequestDto aiRequest = new(request.UserId);

                MorningBriefingResultDto? response = await aiService.GenerateMorningBriefingAsync(aiRequest, cancellationToken);

                return response is not null
                    ? Result<MorningBriefingResultDto>.Success(response)
                    : Result<MorningBriefingResultDto>.Failure(Error.Create("AI morning briefing returned empty response."));
            }
            catch (InvalidOperationException ex)
            {
                return Result<MorningBriefingResultDto>.Failure(
                    Error.Create(ex.InnerException != null ? ex.InnerException.Message : ex.Message));
            }
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.AiBriefing);

            group.MapPost("/generate", async (ISender sender, ClaimsPrincipal user) =>
            {
                Request request = new(UserId: user.GetCurrentUserId());
                Result<MorningBriefingResultDto> result = await sender.Send(request);

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<MorningBriefingResultDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Generate AI-powered morning briefing.")
            .WithDescription("Creates a personalized morning briefing using recent performance, open positions, tilt score, and economic events.")
            .WithTags(Tags.AiBriefing)
            .RequireAuthorization();
        }
    }
}
