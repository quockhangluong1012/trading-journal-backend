using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Modules.AiInsights.Features.V1.Patterns;

public sealed class DiscoverTradePatterns
{
    public sealed record Request(
        DateTime? FromDate,
        DateTime? ToDate,
        int UserId = 0) : ICommand<Result<TradePatternDiscoveryResultDto>>;

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
        : ICommandHandler<Request, Result<TradePatternDiscoveryResultDto>>
    {
        public async Task<Result<TradePatternDiscoveryResultDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            try
            {
                TradePatternDiscoveryRequestDto aiRequest = new(request.FromDate, request.ToDate, request.UserId);

                TradePatternDiscoveryResultDto? response = await aiService.DiscoverTradePatternsAsync(aiRequest, cancellationToken);

                return response is not null
                    ? Result<TradePatternDiscoveryResultDto>.Success(response)
                    : Result<TradePatternDiscoveryResultDto>.Failure(Error.Create("AI pattern discovery returned empty response."));
            }
            catch (InvalidOperationException ex)
            {
                return Result<TradePatternDiscoveryResultDto>.Failure(
                    Error.Create(ex.InnerException != null ? ex.InnerException.Message : ex.Message));
            }
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.AiPatterns);

            group.MapPost("/discover", async (ISender sender, [FromBody] Request request, ClaimsPrincipal user) =>
            {
                Result<TradePatternDiscoveryResultDto> result = await sender.Send(request with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<TradePatternDiscoveryResultDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Discover AI patterns from trade history.")
            .WithDescription("Analyzes closed trades in the selected range and returns multi-variable patterns with evidence and action items.")
            .WithTags(Tags.AiPatterns)
            .RequireAuthorization();
        }
    }
}