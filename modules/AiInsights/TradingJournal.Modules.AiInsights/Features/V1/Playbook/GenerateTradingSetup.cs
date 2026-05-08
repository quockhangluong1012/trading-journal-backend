using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Modules.AiInsights.Features.V1.Playbook;

public sealed class GenerateTradingSetup
{
    public sealed record Request(
        string Prompt,
        int MaxNodes = 8,
        bool DedupeAgainstExisting = true,
        int UserId = 0) : ICommand<Result<TradingSetupGenerationResultDto>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Prompt)
                .NotEmpty()
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Setup prompt is required.")
                .MaximumLength(2000)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Setup prompt cannot exceed 2000 characters.");

            RuleFor(x => x.MaxNodes)
                .InclusiveBetween(3, 12)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Max nodes must be between 3 and 12.");
        }
    }

    public sealed class Handler(IOpenRouterAIService aiService)
        : ICommandHandler<Request, Result<TradingSetupGenerationResultDto>>
    {
        public async Task<Result<TradingSetupGenerationResultDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            try
            {
                TradingSetupGenerationRequestDto aiRequest = new(
                    request.Prompt,
                    request.MaxNodes,
                    request.DedupeAgainstExisting,
                    request.UserId);

                TradingSetupGenerationResultDto? response = await aiService.GenerateTradingSetupAsync(aiRequest, cancellationToken);

                return response is not null
                    ? Result<TradingSetupGenerationResultDto>.Success(response)
                    : Result<TradingSetupGenerationResultDto>.Failure(Error.Create("AI setup generation returned empty response."));
            }
            catch (InvalidOperationException ex)
            {
                return Result<TradingSetupGenerationResultDto>.Failure(
                    Error.Create(ex.InnerException != null ? ex.InnerException.Message : ex.Message));
            }
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.AiPlaybook);

            group.MapPost("/generate-setup", async (ISender sender, [FromBody] Request request, ClaimsPrincipal user) =>
            {
                Result<TradingSetupGenerationResultDto> result = await sender.Send(request with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<TradingSetupGenerationResultDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Generate a trading setup flow preview with AI.")
            .WithDescription("Turns a natural-language setup request into a preview graph that can be reviewed and saved as a trading setup.")
            .WithTags(Tags.AiPlaybook)
            .RequireRateLimiting("ai")
            .RequireAuthorization();
        }
    }
}