using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Modules.AiInsights.Features.V1.Lessons;

public sealed class SuggestLessons
{
    public sealed record Request(
        DateTime? FromDate,
        DateTime? ToDate,
        int UserId = 0) : ICommand<Result<SuggestedLessonsResultDto>>;

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
        : ICommandHandler<Request, Result<SuggestedLessonsResultDto>>
    {
        public async Task<Result<SuggestedLessonsResultDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            try
            {
                SuggestLessonsRequestDto aiRequest = new(request.FromDate, request.ToDate, request.UserId);

                SuggestedLessonsResultDto? response = await aiService.SuggestLessonsAsync(aiRequest, cancellationToken);

                return response is not null
                    ? Result<SuggestedLessonsResultDto>.Success(response)
                    : Result<SuggestedLessonsResultDto>.Failure(Error.Create("AI lesson suggestion returned empty response."));
            }
            catch (InvalidOperationException ex)
            {
                return Result<SuggestedLessonsResultDto>.Failure(
                    Error.Create(ex.InnerException != null ? ex.InnerException.Message : ex.Message));
            }
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.AiLessons);

            group.MapPost("/suggest", async (ISender sender, [FromBody] Request request, ClaimsPrincipal user) =>
            {
                Result<SuggestedLessonsResultDto> result = await sender.Send(request with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<SuggestedLessonsResultDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Suggest AI-generated lessons from recurring trade patterns.")
            .WithDescription("Analyzes recent closed trades, avoids duplicate lessons, and returns proposed lessons with linked trade ids.")
            .WithTags(Tags.AiLessons)
            .RequireAuthorization();
        }
    }
}