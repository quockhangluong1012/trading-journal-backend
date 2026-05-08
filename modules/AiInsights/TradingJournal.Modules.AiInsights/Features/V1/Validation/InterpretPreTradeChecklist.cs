using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Modules.AiInsights.Features.V1.Validation;

public sealed class InterpretPreTradeChecklist
{
    public sealed record Request(
        int ChecklistModelId,
        string Input,
        int UserId = 0) : ICommand<Result<PreTradeChecklistInterpretationResultDto>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.ChecklistModelId)
                .GreaterThan(0)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Checklist model is required.");

            RuleFor(x => x.Input)
                .NotEmpty()
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Checklist input is required.")
                .MaximumLength(4000)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Checklist input cannot exceed 4000 characters.");
        }
    }

    public sealed class Handler(IOpenRouterAIService aiService)
        : ICommandHandler<Request, Result<PreTradeChecklistInterpretationResultDto>>
    {
        public async Task<Result<PreTradeChecklistInterpretationResultDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            try
            {
                PreTradeChecklistInterpretationRequestDto aiRequest = new(
                    request.ChecklistModelId,
                    request.Input,
                    request.UserId);

                PreTradeChecklistInterpretationResultDto? response = await aiService.InterpretPreTradeChecklistAsync(aiRequest, cancellationToken);

                return response is not null
                    ? Result<PreTradeChecklistInterpretationResultDto>.Success(response)
                    : Result<PreTradeChecklistInterpretationResultDto>.Failure(Error.Create("AI checklist interpretation returned empty response."));
            }
            catch (InvalidOperationException ex)
            {
                return Result<PreTradeChecklistInterpretationResultDto>.Failure(
                    Error.Create(ex.InnerException != null ? ex.InnerException.Message : ex.Message));
            }
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.AiValidation);

            group.MapPost("/interpret-checklist", async (ISender sender, [FromBody] Request request, ClaimsPrincipal user) =>
            {
                Result<PreTradeChecklistInterpretationResultDto> result = await sender.Send(request with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<PreTradeChecklistInterpretationResultDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Interpret natural-language pre-trade notes against a checklist model.")
            .WithDescription("Uses AI to map a trader's freeform checklist notes to the currently selected checklist model and return reviewable suggestions.")
            .WithTags(Tags.AiValidation)
            .RequireRateLimiting("ai")
            .RequireAuthorization();
        }
    }
}