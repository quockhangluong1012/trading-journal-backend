 

namespace TradingJournal.Modules.Auth.Features.V1.Staffs;

public sealed class CreateStaff
{
    internal sealed record Request(string Email, string Password, string FullName, bool IsActive) : ICommand<Result<int>>;

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
            RuleFor(x => x.FullName).NotEmpty();
        }
    }

    internal sealed class Handler(IAuthDbContext context) : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            bool exists = await context.Staffs.AnyAsync(s => s.Email == request.Email, cancellationToken);
            if (exists)
                return Result<int>.Failure(Error.Create("Email already registered."));

            Staff newStaff = new()
            {
                Id = 0,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                FullName = request.FullName,
                IsActive = request.IsActive,
                CreatedDate = DateTime.UtcNow
            };

            await context.Staffs.AddAsync(newStaff, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(newStaff.Id);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup($"{AuthConstants.Endpoints.AuthBase}/staffs");

            group.MapPost("/", async ([FromBody] Request request, ISender sender) =>
            {
                var result = await sender.Send(request);
                return result.IsSuccess ? Results.Created($"{AuthConstants.Endpoints.AuthBase}/staffs/{result.Value}", result)
                                        : Results.BadRequest(result);
            })
            .Produces<Result<int>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Create a new staff member.")
            .WithTags("Staffs")
            .RequireAuthorization("AdminOnly");
        }
    }
}
