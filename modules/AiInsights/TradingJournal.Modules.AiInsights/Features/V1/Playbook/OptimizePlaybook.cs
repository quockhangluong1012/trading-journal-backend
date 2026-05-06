using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Modules.AiInsights.Features.V1.Playbook;

public sealed class OptimizePlaybook
{
    public sealed record Request(
        DateTime? FromDate,
        DateTime? ToDate,
        int UserId = 0) : ICommand<Result<PlaybookOptimizationResultDto>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.ToDate)
                .Must((request, toDate) => !request.FromDate.HasValue || !toDate.HasValue || toDate.Value.Date >= request.FromDate.Value.Date)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("To date must be on or after from date.");
        }
    }

    public sealed class Handler(IOpenRouterAIService aiService)
        : ICommandHandler<Request, Result<PlaybookOptimizationResultDto>>
    {
        public async Task<Result<PlaybookOptimizationResultDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            try
            {
                PlaybookOptimizationRequestDto aiRequest = new(request.FromDate, request.ToDate, request.UserId);

                PlaybookOptimizationResultDto? response = await aiService.OptimizePlaybookAsync(aiRequest, cancellationToken);

                return response is not null
                    ? Result<PlaybookOptimizationResultDto>.Success(response)
                    : Result<PlaybookOptimizationResultDto>.Failure(Error.Create("AI playbook optimizer returned empty response."));
            }
            catch (InvalidOperationException ex)
            {
                return Result<PlaybookOptimizationResultDto>.Failure(
                    Error.Create(ex.InnerException != null ? ex.InnerException.Message : ex.Message));
            }
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.AiPlaybook);

            group.MapPost("/optimize", async (ISender sender, [FromBody] Request request, ClaimsPrincipal user) =>
            {
                Result<PlaybookOptimizationResultDto> result = await sender.Send(request with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<PlaybookOptimizationResultDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Optimize playbook setups with AI.")
            .WithDescription("Analyzes setup performance over the selected range and recommends which setups to prioritize, refine, or retire.")
            .WithTags(Tags.AiPlaybook)
            .RequireAuthorization();
        }
    }
}