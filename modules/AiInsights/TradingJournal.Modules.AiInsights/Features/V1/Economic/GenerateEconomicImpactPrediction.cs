using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Modules.AiInsights.Features.V1.Economic;

public sealed class GenerateEconomicImpactPrediction
{
    public sealed record Request(
        string Symbol,
        int ProximityMinutes = 30,
        int UserId = 0) : ICommand<Result<AiEconomicImpactPredictorResultDto>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Symbol)
                .NotEmpty()
                .MaximumLength(7)
                .Matches("^[A-Za-z]{3}([/-]?)[A-Za-z]{3}$")
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Economic impact prediction currently supports FX pair symbols like EURUSD or EUR/USD.");

            RuleFor(x => x.ProximityMinutes)
                .InclusiveBetween(5, 120)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Proximity window must be between 5 and 120 minutes.");
        }
    }

    public sealed class Handler(IOpenRouterAIService aiService)
        : ICommandHandler<Request, Result<AiEconomicImpactPredictorResultDto>>
    {
        public async Task<Result<AiEconomicImpactPredictorResultDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            try
            {
                AiEconomicImpactPredictorResultDto? response = await aiService.GenerateEconomicImpactPredictionAsync(
                    new AiEconomicImpactPredictorRequestDto(request.Symbol, request.ProximityMinutes, request.UserId),
                    cancellationToken);

                return response is not null
                    ? Result<AiEconomicImpactPredictorResultDto>.Success(response)
                    : Result<AiEconomicImpactPredictorResultDto>.Failure(Error.Create("AI economic impact predictor returned empty response."));
            }
            catch (InvalidOperationException ex)
            {
                return Result<AiEconomicImpactPredictorResultDto>.Failure(
                    Error.Create(ex.InnerException != null ? ex.InnerException.Message : ex.Message));
            }
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.AiEconomic);

            group.MapPost("/predict-impact", async (ISender sender, [FromBody] Request request, ClaimsPrincipal user) =>
            {
                Result<AiEconomicImpactPredictorResultDto> result = await sender.Send(request with { UserId = user.GetCurrentUserId() });

                return ToHttpResult(result);
            })
            .Produces<Result<AiEconomicImpactPredictorResultDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Generate AI economic event impact prediction.")
            .WithDescription("Predicts the trading impact of current economic event conditions for a symbol using event proximity and historical event-trading behavior.")
            .WithTags(Tags.AiEconomic)
            .RequireAuthorization();
        }

        private static IResult ToHttpResult(Result<AiEconomicImpactPredictorResultDto> result)
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