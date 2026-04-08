 

namespace TradingJournal.Modules.Auth.Features.V1.Staffs;

public sealed class DeleteStaff
{
    internal sealed record Request(int Id) : ICommand<Result<bool>>;

    internal sealed class Handler(IAuthDbContext context) : ICommandHandler<Request, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Request request, CancellationToken cancellationToken)
        {
            var staff = await context.Staffs.FindAsync([request.Id], cancellationToken);
            if (staff == null)
                return Result<bool>.Failure(Error.Create("Staff not found."));

            context.Staffs.Remove(staff);
            await context.SaveChangesAsync(cancellationToken);

            return Result<bool>.Success(true);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup($"{AuthConstants.Endpoints.AuthBase}/staffs");

            group.MapDelete("/{id:int}", async (int id, ISender sender) =>
            {
                var result = await sender.Send(new Request(id));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<bool>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Delete a staff member.")
            .WithTags("Staffs")
            .RequireAuthorization("AdminOnly");
        }
    }
}
