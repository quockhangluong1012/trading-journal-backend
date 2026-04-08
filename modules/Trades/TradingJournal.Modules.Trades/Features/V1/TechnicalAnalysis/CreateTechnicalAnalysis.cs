namespace TradingJournal.Modules.Trades.Features.V1.TechnicalAnalysis;

public sealed class CreateTechnicalAnalysis
{
    internal sealed record Request(string Name, string? ShortName, string? Description, int UserId = 0) : ICommand<Result<int>>;

    internal class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Technical Analysis Name cannot be null.")
                .NotEmpty().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Technical Analysis Name cannot be empty.");
        }
    }

    internal sealed class Handler(ITradeDbContext context) : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            if (request.UserId == 0)
            {
                return Result<int>.Failure(Error.Create("Unauthorized."));
            }

            Domain.TechnicalAnalysis technicalAnalysis = new()
            {
                Id = 0,
                Name = request.Name,
                ShortName = request.ShortName ?? string.Empty,
                Description = request.Description,
                CreatedBy = request.UserId,
            };

            await context.TechnicalAnalyses.AddAsync(technicalAnalysis, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(technicalAnalysis.Id);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TechnicalAnalysis);

            group.MapPost("/", async ([FromBody] Request request, ISender sender) =>
            {
                Result<int> result = await sender.Send(request);

                return result.IsSuccess ? Results.Ok(result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<int>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Create a new technical analysis.")
            .WithDescription("Creates a new technical analysis.")
            .WithTags(Tags.TechnicalAnalysis)
            .RequireAuthorization();
        }
    }
}