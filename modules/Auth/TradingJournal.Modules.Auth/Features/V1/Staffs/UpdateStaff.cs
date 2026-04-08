 

namespace TradingJournal.Modules.Auth.Features.V1.Staffs;

public sealed class UpdateStaff
{
    internal sealed record Request(int Id, string FullName, string? Password, bool IsActive) : ICommand<Result<bool>>;

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.FullName).NotEmpty();
            When(x => !string.IsNullOrEmpty(x.Password), () =>
            {
                RuleFor(x => x.Password).MinimumLength(6);
            });
        }
    }

    internal sealed class Handler(IAuthDbContext context) : ICommandHandler<Request, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Request request, CancellationToken cancellationToken)
        {
            var staff = await context.Staffs.FindAsync([request.Id], cancellationToken);
            if (staff == null)
                return Result<bool>.Failure(Error.Create("Staff not found."));

            staff.FullName = request.FullName;
            staff.IsActive = request.IsActive;

            if (!string.IsNullOrEmpty(request.Password))
            {
                staff.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result<bool>.Success(true);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup($"{AuthConstants.Endpoints.AuthBase}/staffs");

            group.MapPut("/{id:int}", async (int id, [FromBody] Request request, ISender sender) =>
            {
                if (id != request.Id) return Results.BadRequest("ID mismatch.");
                var result = await sender.Send(request);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<bool>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Update a staff member.")
            .WithTags("Staffs")
            .RequireAuthorization("AdminOnly");
        }
    }
}
