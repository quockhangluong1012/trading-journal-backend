using TradingJournal.Modules.Backtest.Dto;

namespace TradingJournal.Modules.Backtest.Features.V1.DrawingTemplates;

public sealed class GetDrawingTemplates
{
    public record Request : IQuery<Result<List<ChartDrawingTemplateDto>>>
    {
        public int UserId { get; set; }
    }

    internal sealed class Handler(IBacktestDbContext context)
        : IQueryHandler<Request, Result<List<ChartDrawingTemplateDto>>>
    {
        public async Task<Result<List<ChartDrawingTemplateDto>>> Handle(Request request, CancellationToken cancellationToken)
        {
            List<ChartDrawingTemplateDto> templates = await context.ChartDrawingTemplates
                .AsNoTracking()
                .Where(t => t.CreatedBy == request.UserId && !t.IsDisabled)
                .OrderBy(t => t.CreatedDate)
                .ThenBy(t => t.Id)
                .Select(t => new ChartDrawingTemplateDto(
                    t.Id,
                    $"custom-{t.Id}",
                    t.Name,
                    t.StyleJson,
                    t.Tool,
                    t.Text,
                    t.CreatedDate))
                .ToListAsync(cancellationToken);

            return Result<List<ChartDrawingTemplateDto>>.Success(templates);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.DrawingTemplates);

            group.MapGet("/", async (ClaimsPrincipal user, ISender sender) =>
            {
                Result<List<ChartDrawingTemplateDto>> result = await sender.Send(new Request
                {
                    UserId = user.GetCurrentUserId()
                });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<List<ChartDrawingTemplateDto>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get saved backtest drawing style templates.")
            .WithDescription("Retrieves account-level custom drawing templates that can be reused across backtest sessions.")
            .WithTags(Tags.BacktestDrawingTemplates)
            .RequireAuthorization();
        }
    }
}
