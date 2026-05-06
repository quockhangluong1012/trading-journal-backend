using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Modules.AiInsights.Features.V1.Search;

public sealed class SearchTradesNaturalLanguage
{
    public sealed record Request(
        string Query,
        int UserId = 0) : ICommand<Result<NaturalLanguageTradeSearchResultDto>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Query)
                .NotEmpty()
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Search query is required.");

            RuleFor(x => x.Query)
                .MaximumLength(500)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Search query cannot exceed 500 characters.");
        }
    }

    public sealed class Handler(IOpenRouterAIService aiService)
        : ICommandHandler<Request, Result<NaturalLanguageTradeSearchResultDto>>
    {
        public async Task<Result<NaturalLanguageTradeSearchResultDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            try
            {
                NaturalLanguageTradeSearchRequestDto aiRequest = new(request.Query, request.UserId);

                NaturalLanguageTradeSearchResultDto? response = await aiService.SearchTradesNaturalLanguageAsync(aiRequest, cancellationToken);

                return response is not null
                    ? Result<NaturalLanguageTradeSearchResultDto>.Success(response)
                    : Result<NaturalLanguageTradeSearchResultDto>.Failure(Error.Create("AI trade search returned empty response."));
            }
            catch (InvalidOperationException ex)
            {
                return Result<NaturalLanguageTradeSearchResultDto>.Failure(
                    Error.Create(ex.InnerException != null ? ex.InnerException.Message : ex.Message));
            }
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.AiSearch);

            group.MapPost("/trades", async (ISender sender, [FromBody] Request request, ClaimsPrincipal user) =>
            {
                Result<NaturalLanguageTradeSearchResultDto> result = await sender.Send(request with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<NaturalLanguageTradeSearchResultDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Translate a natural-language trade search into structured filters.")
            .WithDescription("Uses AI to interpret a natural-language trade-history query and returns structured filters compatible with the history view.")
            .WithTags(Tags.AiSearch)
            .RequireAuthorization();
        }
    }
}