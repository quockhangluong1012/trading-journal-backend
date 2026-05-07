using System.Globalization;
using TradingJournal.Modules.Trades.Services;

namespace TradingJournal.Modules.Trades.Features.V1.Screenshot;

public sealed class UploadScreenShoot
{
    private const long MaxUploadSizeBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/jpg",
        "image/gif",
        "image/webp"
    };

    public sealed record Request(IFormFile File) : ICommand<Result<string>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.File)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("File cannot be null.")
                .Must(file => file.Length > 0)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("File cannot be empty.")
                .Must(file => file.Length <= MaxUploadSizeBytes)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage($"File size must not exceed {MaxUploadSizeBytes / (1024 * 1024)} MB.")
                .Must(file => AllowedMimeTypes.Contains(file.ContentType))
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage($"File must be one of: {string.Join(", ", AllowedMimeTypes)}.");
        }
    }

    public sealed class Handler(IScreenshotService screenshotService) : ICommandHandler<Request, Result<string>>
    {
        public async Task<Result<string>> Handle(Request request, CancellationToken cancellationToken)
        {
            if (request.File == null)
            {
                return Result<string>.Failure(Error.Create("File cannot be null."));
            }

            await using MemoryStream stream = new();
            await request.File.CopyToAsync(stream, cancellationToken);

            string base64Payload = Convert.ToBase64String(stream.ToArray());
            string dataUri = string.Create(
                CultureInfo.InvariantCulture,
                $"data:{request.File.ContentType};base64,{base64Payload}");

            try
            {
                string imageUrl = await screenshotService.SaveScreenshotAsync(dataUri, cancellationToken);
                return Result<string>.Success(imageUrl);
            }
            catch (InvalidOperationException ex)
            {
                return Result<string>.Failure(Error.Create(ex.Message));
            }
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Screenshots);

            group.MapPost("/upload", async (Request request, ISender sender) =>
            {
                var result = await sender.Send(request);

                return result.IsSuccess ? Results.Ok(result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<string>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Upload a screenshot.")
            .WithDescription("Uploads a screenshot.")
            .WithTags(Tags.TradingScreenshoots)
            .RequireAuthorization();
        }
    }
}