using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Trades.Features.V1.Templates;

public sealed class GetTemplateById
{
    public record Request(int Id) : IQuery<Result<TradeTemplateViewModel>>
    {
        public int UserId { get; set; }
    }

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Template Id must be greater than 0.");
        }
    }

    public sealed class Handler(ITradeDbContext context) : IQueryHandler<Request, Result<TradeTemplateViewModel>>
    {
        public async Task<Result<TradeTemplateViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            TradeTemplate? template = await context.TradeTemplates
                .AsNoTracking()
                .Include(t => t.TradingZone)
                .FirstOrDefaultAsync(t => t.Id == request.Id && t.CreatedBy == request.UserId && !t.IsDisabled,
                    cancellationToken);

            if (template is null)
            {
                return Result<TradeTemplateViewModel>.Failure(Error.Create("Template not found."));
            }

            return Result<TradeTemplateViewModel>.Success(GetTemplates.Handler.MapToViewModel(template));
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradeTemplates);

            group.MapGet("/{id:int}", async (int id, ISender sender, ClaimsPrincipal user) =>
            {
                Request request = new(id) { UserId = user.GetCurrentUserId() };
                Result<TradeTemplateViewModel> result = await sender.Send(request);

                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .Produces<Result<TradeTemplateViewModel>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Get a trade template by ID.")
            .WithDescription("Retrieves a single trade template by its ID for the current user.")
            .WithTags(Tags.TradeTemplates)
            .RequireAuthorization();
        }
    }
}
