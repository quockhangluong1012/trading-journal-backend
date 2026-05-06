using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Modules.AiInsights.Features.V1.Risk;

public sealed class GenerateRiskAdvice
{
    public sealed record Request(int UserId = 0) : ICommand<Result<AiRiskAdvisorResultDto>>;

    public sealed class Handler(IOpenRouterAIService aiService)
        : ICommandHandler<Request, Result<AiRiskAdvisorResultDto>>
    {
        public async Task<Result<AiRiskAdvisorResultDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            try
            {
                AiRiskAdvisorResultDto? response = await aiService.GenerateRiskAdvisorAsync(
                    new AiRiskAdvisorRequestDto(request.UserId),
                    cancellationToken);

                return response is not null
                    ? Result<AiRiskAdvisorResultDto>.Success(response)
                    : Result<AiRiskAdvisorResultDto>.Failure(Error.Create("AI risk advisor returned empty response."));
            }
            catch (InvalidOperationException ex)
            {
                return Result<AiRiskAdvisorResultDto>.Failure(
                    Error.Create(ex.InnerException != null ? ex.InnerException.Message : ex.Message));
            }
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.AiRisk);

            group.MapPost("/generate", async (ISender sender, ClaimsPrincipal user) =>
            {
                Result<AiRiskAdvisorResultDto> result = await sender.Send(new Request(user.GetCurrentUserId()));

                return ToHttpResult(result);
            })
            .Produces<Result<AiRiskAdvisorResultDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Generate AI risk advisor guidance.")
            .WithDescription("Analyzes current risk limits, active exposure, and recent trade behavior to return actionable AI risk guidance.")
            .WithTags(Tags.AiRisk)
            .RequireAuthorization();
        }

        private static IResult ToHttpResult(Result<AiRiskAdvisorResultDto> result)
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