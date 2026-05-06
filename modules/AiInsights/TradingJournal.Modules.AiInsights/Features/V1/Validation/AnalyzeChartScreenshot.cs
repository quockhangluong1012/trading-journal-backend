using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Modules.AiInsights.Features.V1.Validation;

public sealed class AnalyzeChartScreenshot
{
    internal const int MaxScreenshotCount = 3;
    internal const int MaxDecodedScreenshotBytes = 5 * 1024 * 1024;
    internal const int MaxScreenshotSourceLength = ((MaxDecodedScreenshotBytes + 2) / 3 * 4) + 64;

    public sealed record Request(
        string Asset,
        string Position,
        decimal? EntryPrice,
        decimal? StopLoss,
        decimal? TargetTier1,
        string? TradingZone,
        string? Notes,
        List<string> Screenshots,
        int UserId = 0) : ICommand<Result<ChartScreenshotAnalysisResultDto>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Asset)
                .NotEmpty()
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Asset is required.");

            RuleFor(x => x.Position)
                .NotEmpty()
                .Must(position => position.Equals("Long", StringComparison.OrdinalIgnoreCase)
                    || position.Equals("Short", StringComparison.OrdinalIgnoreCase))
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Position must be Long or Short.");

            RuleFor(x => x.Screenshots)
                .NotEmpty()
                .Must(screenshots => screenshots.Count <= MaxScreenshotCount)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage($"Upload between 1 and {MaxScreenshotCount} screenshots for chart analysis.");

            RuleForEach(x => x.Screenshots)
                .Must(IsSupportedScreenshotSource)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Screenshots must be PNG, JPG, or WebP data URLs or HTTPS image URLs.");
        }

        private static bool IsSupportedScreenshotSource(string screenshot)
        {
            if (string.IsNullOrWhiteSpace(screenshot))
            {
                return false;
            }

            if (screenshot.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                return screenshot.Length <= MaxScreenshotSourceLength;
            }

            return Uri.TryCreate(screenshot, UriKind.Absolute, out Uri? uri)
                && uri.Scheme == Uri.UriSchemeHttps;
        }
    }

    public sealed class Handler(IOpenRouterAIService aiService)
        : ICommandHandler<Request, Result<ChartScreenshotAnalysisResultDto>>
    {
        public async Task<Result<ChartScreenshotAnalysisResultDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            try
            {
                ChartScreenshotAnalysisRequestDto aiRequest = new(
                    request.Asset,
                    request.Position,
                    request.EntryPrice,
                    request.StopLoss,
                    request.TargetTier1,
                    request.TradingZone,
                    request.Notes,
                    request.Screenshots,
                    request.UserId);

                ChartScreenshotAnalysisResultDto? response = await aiService.AnalyzeChartScreenshotAsync(aiRequest, cancellationToken);

                return response is not null
                    ? Result<ChartScreenshotAnalysisResultDto>.Success(response)
                    : Result<ChartScreenshotAnalysisResultDto>.Failure(Error.Create("AI chart screenshot analysis returned empty response."));
            }
            catch (InvalidOperationException ex)
            {
                return Result<ChartScreenshotAnalysisResultDto>.Failure(
                    Error.Create(ex.InnerException != null ? ex.InnerException.Message : ex.Message));
            }
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.AiValidation);

            group.MapPost("/analyze-chart", async (ISender sender, [FromBody] Request request, ClaimsPrincipal user) =>
            {
                Result<ChartScreenshotAnalysisResultDto> result = await sender.Send(request with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<ChartScreenshotAnalysisResultDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Analyze pre-entry chart screenshots with AI.")
            .WithDescription("Uses the vision-capable AI flow to read chart screenshots and return market structure, confluences, and warnings before entry.")
            .WithTags(Tags.AiValidation)
            .RequireAuthorization();
        }
    }
}