namespace TradingJournal.Modules.Psychology.Features.V1.Psychology;

public sealed class DeletePsychologyJournal
{
    public sealed class Request : ICommand<Result<bool>>
    {
        public int Id { get; set; }
        public int UserId { get; set; }
    }

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() 
        {
            RuleFor(x => x.Id)
                .NotEmpty()
                .GreaterThan(0)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Id is required");
        }
    }

    internal sealed class Handler(IPsychologyDbContext context) : ICommandHandler<Request, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Request request, CancellationToken cancellationToken)
        {
            PsychologyJournal? psychologyJournal = await context.PsychologyJournals.FirstOrDefaultAsync(x => x.Id == request.Id && x.CreatedBy == request.UserId, cancellationToken);
            
            if (psychologyJournal is null)
            {
                return Result<bool>.Failure(Error.Create("Psychology journal not found."));
            }

            context.PsychologyJournals.Remove(psychologyJournal);

            await context.SaveChangesAsync(cancellationToken);

            return Result<bool>.Success(true);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/psychology-journals");

            group.MapDelete("/{id}", async (int id, ClaimsPrincipal user, ISender sender) =>
            {
                Result<bool> result = await sender.Send(new Request { Id = id, UserId = user.GetCurrentUserId() });
                return result;
            })
            .Produces<Result<bool>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Delete a psychology journal.")
            .WithDescription("Deletes a psychology journal")
            .WithTags(Tags.PsychologyJournal)
            .RequireAuthorization();
        }
    }
}
