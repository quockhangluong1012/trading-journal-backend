using Microsoft.AspNetCore.Hosting;

namespace TradingJournal.Modules.Trades.Features.V1.Screenshot;

public sealed class UploadScreenShoot
{
    public sealed record Request(IFormFile File) : ICommand<Result<string>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.File)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("File cannot be null.");
        }
    }

    public sealed class Handler(IWebHostEnvironment env, IHttpContextAccessor httpContextAccessor) : ICommandHandler<Request, Result<string>>
    {
        public async Task<Result<string>> Handle(Request request, CancellationToken cancellationToken)
        {
            if (request.File == null) return Result<string>.Failure(Error.Create("File cannot be null."));

            var path = Path.Combine(env.ContentRootPath, "wwwroot", "screenshots");

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var fileName = Guid.NewGuid().ToString() + ".png";
            
            var filePath = Path.Combine(path, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await request.File.CopyToAsync(stream);
            }

            HttpContext? httpContext = httpContextAccessor.HttpContext;

            string imageUrl = string.Empty;

            if (httpContext != null)
            {
                imageUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/screenshots/{fileName}";
            }
            return Result<string>.Success(imageUrl);
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