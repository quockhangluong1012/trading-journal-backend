namespace TradingJournal.Modules.Trades.Features.V1.TechnicalAnalysis;

public sealed class UpdateTechnicalAnalysis
{
    internal sealed record Request(int Id, string Name, string? ShortName, string? Description, int UserId = 0) : ICommand<Result<bool>>;

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Id)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0).WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Technical Analysis Id must be greater than 0.");

            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Technical Analysis Name is required.");
        }
    }

    internal sealed class Handler(ITradeDbContext context) : ICommandHandler<Request, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Request request, CancellationToken cancellationToken)
        {
            Domain.TechnicalAnalysis? technicalAnalysis = await context.TechnicalAnalyses
                .FirstOrDefaultAsync(x => x.Id == request.Id && x.CreatedBy == request.UserId, cancellationToken);

            if (technicalAnalysis is null)
            {
                return Result<bool>.Failure(Error.NotFound);
            }

            technicalAnalysis.Name = request.Name;
            technicalAnalysis.ShortName = request.ShortName ?? string.Empty;
            technicalAnalysis.Description = request.Description;

            await context.SaveChangesAsync(cancellationToken);

            return Result<bool>.Success(true);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TechnicalAnalysis);

            group.MapPut("/", async ([FromBody] Request request, ISender sender) =>
            {
                Result<bool> result = await sender.Send(request);

                return result.IsSuccess ? Results.Ok(result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<bool>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Update a technical analysis.")
            .WithDescription("Updates a technical analysis.")
            .WithTags(Tags.TechnicalAnalysis)
            .RequireAuthorization("AdminOnly");
        }
    }
}
