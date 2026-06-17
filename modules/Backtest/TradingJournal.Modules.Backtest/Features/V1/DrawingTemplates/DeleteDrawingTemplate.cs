namespace TradingJournal.Modules.Backtest.Features.V1.DrawingTemplates;

public sealed class DeleteDrawingTemplate
{
    public record Request(int Id) : ICommand<Result<bool>>
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

    internal sealed class Handler(IBacktestDbContext context)
        : ICommandHandler<Request, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Request request, CancellationToken cancellationToken)
        {
            if (request.UserId <= 0)
            {
                return Result<bool>.Failure(Error.Create("Unauthorized."));
            }

            ChartDrawingTemplate? template = await context.ChartDrawingTemplates
                .FirstOrDefaultAsync(t => t.Id == request.Id
                                          && t.CreatedBy == request.UserId
                                          && !t.IsDisabled, cancellationToken);

            if (template is null)
            {
                return Result<bool>.Failure(Error.Create("Template not found."));
            }

            template.IsDisabled = true;
            int rows = await context.SaveChangesAsync(cancellationToken);

            return rows > 0
                ? Result<bool>.Success(true)
                : Result<bool>.Failure(Error.Create("Failed to delete drawing template."));
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.DrawingTemplates);

            group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal user, ISender sender) =>
            {
                Result<bool> result = await sender.Send(new Request(id)
                {
                    UserId = user.GetCurrentUserId()
                });

                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .Produces<Result<bool>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Delete a reusable backtest drawing style template.")
            .WithTags(Tags.BacktestDrawingTemplates)
            .RequireAuthorization();
        }
    }
}
