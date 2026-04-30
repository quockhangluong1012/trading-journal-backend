using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Trades.Features.V1.Templates;

public sealed class DeleteTemplate
{
    public record Request(int Id) : ICommand<Result<bool>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Template Id must be greater than 0.");
        }
    }

    public sealed class Handler(ITradeDbContext context, IHttpContextAccessor httpContextAccessor)
        : ICommandHandler<Request, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Request request, CancellationToken cancellationToken)
        {
            int userId = httpContextAccessor.HttpContext?.User.GetCurrentUserId() ?? 0;
            if (userId <= 0)
                return Result<bool>.Failure(Error.Create("Unauthorized."));

            TradeTemplate? template = await context.TradeTemplates
                .FirstOrDefaultAsync(t => t.Id == request.Id && t.CreatedBy == userId && !t.IsDisabled, cancellationToken);

            if (template is null)
                return Result<bool>.Failure(Error.Create("Template not found."));

            template.IsDisabled = true;
            int rows = await context.SaveChangesAsync(cancellationToken);

            return rows > 0 ? Result<bool>.Success(true) : Result<bool>.Failure(Error.Create("Failed to delete."));
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradeTemplates);

            group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal user, ISender sender) =>
            {
                Result<bool> result = await sender.Send(new Request(id));
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .Produces<Result<bool>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Delete a trade template.")
            .WithTags(Tags.TradeTemplates)
            .RequireAuthorization();
        }
    }
}
