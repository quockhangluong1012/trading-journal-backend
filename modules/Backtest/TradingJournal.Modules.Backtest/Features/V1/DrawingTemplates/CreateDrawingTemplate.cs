using System.Text.Json;
using TradingJournal.Modules.Backtest.Dto;

namespace TradingJournal.Modules.Backtest.Features.V1.DrawingTemplates;

public sealed class CreateDrawingTemplate
{
    public record Request(
        string Label,
        string StyleJson,
        string? Tool,
        string? Text) : ICommand<Result<ChartDrawingTemplateDto>>
    {
        public int UserId { get; set; }
    }

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Label)
                .NotEmpty().WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Template name is required.")
                .MaximumLength(120).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Template name cannot exceed 120 characters.");

            RuleFor(x => x.StyleJson)
                .NotEmpty().WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Template style is required.")
                .Must(BeValidJson).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Template style must be valid JSON.");

            RuleFor(x => x.Tool)
                .MaximumLength(50).WithErrorCode(nameof(HttpStatusCode.BadRequest));

            RuleFor(x => x.Text)
                .MaximumLength(500).WithErrorCode(nameof(HttpStatusCode.BadRequest));
        }

        private static bool BeValidJson(string styleJson)
        {
            try
            {
                using JsonDocument _ = JsonDocument.Parse(styleJson);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }

    internal sealed class Handler(IBacktestDbContext context)
        : ICommandHandler<Request, Result<ChartDrawingTemplateDto>>
    {
        public async Task<Result<ChartDrawingTemplateDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            if (request.UserId <= 0)
            {
                return Result<ChartDrawingTemplateDto>.Failure(Error.Create("Unauthorized."));
            }

            string name = request.Label.Trim();
            string styleJson = request.StyleJson.Trim();

            ChartDrawingTemplate template = new()
            {
                Id = 0,
                CreatedBy = request.UserId,
                Name = name,
                StyleJson = styleJson,
                Tool = string.IsNullOrWhiteSpace(request.Tool) ? null : request.Tool.Trim(),
                Text = string.IsNullOrWhiteSpace(request.Text) ? null : request.Text.Trim()
            };

            await context.ChartDrawingTemplates.AddAsync(template, cancellationToken);
            int rows = await context.SaveChangesAsync(cancellationToken);

            if (rows <= 0)
            {
                return Result<ChartDrawingTemplateDto>.Failure(Error.Create("Failed to create drawing template."));
            }

            return Result<ChartDrawingTemplateDto>.Success(ToDto(template));
        }

        private static ChartDrawingTemplateDto ToDto(ChartDrawingTemplate template)
        {
            return new ChartDrawingTemplateDto(
                template.Id,
                $"custom-{template.Id}",
                template.Name,
                template.StyleJson,
                template.Tool,
                template.Text,
                template.CreatedDate);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.DrawingTemplates);

            group.MapPost("/", async ([FromBody] Request request, ClaimsPrincipal user, ISender sender) =>
            {
                Result<ChartDrawingTemplateDto> result = await sender.Send(request with
                {
                    UserId = user.GetCurrentUserId()
                });

                return result.IsSuccess
                    ? Results.Created($"{ApiGroup.V1.DrawingTemplates}/{result.Value.Id}", result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<ChartDrawingTemplateDto>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Save a reusable backtest drawing style template.")
            .WithDescription("Stores a custom drawing style template at the account level so it can be reused in other backtest sessions.")
            .WithTags(Tags.BacktestDrawingTemplates)
            .RequireAuthorization();
        }
    }
}
