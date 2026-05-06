using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Modules.AiInsights.Features.V1.Validation;

public sealed class ValidateTradeSetup
{
    public sealed record Request(
        string Asset,
        string Position,
        decimal EntryPrice,
        decimal StopLoss,
        decimal TargetTier1,
        decimal? TargetTier2,
        decimal? TargetTier3,
        int ConfidenceLevel,
        string? TradingZone,
        List<string>? TechnicalAnalysisTags,
        string? ChecklistStatus,
        List<string>? EmotionTags,
        string? Notes,
        int UserId = 0) : ICommand<Result<PreTradeValidationResultDto>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Asset)
                .NotEmpty()
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Asset is required.");

            RuleFor(x => x.EntryPrice)
                .GreaterThan(0)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Entry price must be greater than 0.");

            RuleFor(x => x.StopLoss)
                .GreaterThan(0)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Stop loss must be greater than 0.");

            RuleFor(x => x.TargetTier1)
                .GreaterThan(0)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("At least one target price is required.");
        }
    }

    public sealed class Handler(IOpenRouterAIService aiService) : ICommandHandler<Request, Result<PreTradeValidationResultDto>>
    {
        public async Task<Result<PreTradeValidationResultDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            try
            {
                PreTradeValidationRequestDto aiRequest = new(
                    request.Asset,
                    request.Position,
                    request.EntryPrice,
                    request.StopLoss,
                    request.TargetTier1,
                    request.TargetTier2,
                    request.TargetTier3,
                    request.ConfidenceLevel,
                    request.TradingZone,
                    request.TechnicalAnalysisTags,
                    request.ChecklistStatus,
                    request.EmotionTags,
                    request.Notes,
                    request.UserId);

                PreTradeValidationResultDto? response = await aiService.ValidateTradeSetupAsync(aiRequest, cancellationToken);

                return response is not null
                    ? Result<PreTradeValidationResultDto>.Success(response)
                    : Result<PreTradeValidationResultDto>.Failure(Error.Create("AI validation returned empty response."));
            }
            catch (InvalidOperationException ex)
            {
                return Result<PreTradeValidationResultDto>.Failure(
                    Error.Create(ex.InnerException != null ? ex.InnerException.Message : ex.Message));
            }
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.AiValidation);

            group.MapPost("/validate", async (ISender sender, [FromBody] Request request, ClaimsPrincipal user) =>
            {
                Result<PreTradeValidationResultDto> result = await sender.Send(request with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<PreTradeValidationResultDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Validate a trade setup with AI before entry.")
            .WithDescription("Sends trade setup data to AI for ICT methodology validation, R:R assessment, and emotional readiness check.")
            .WithTags(Tags.AiValidation)
            .RequireAuthorization();
        }
    }
}
