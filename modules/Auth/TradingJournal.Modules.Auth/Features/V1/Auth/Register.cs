namespace TradingJournal.Modules.Auth.Features.V1.Auth;

public sealed class Register
{
    internal sealed record Request(string Email, string Password, string FullName) : ICommand<Result<int>>;

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Email)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Email is required.")
                .EmailAddress().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Invalid email format.");

            RuleFor(x => x.Password)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Password is required.")
                .MinimumLength(8).WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Password must be at least 8 characters.");

            RuleFor(x => x.FullName)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Full name is required.");
        }
    }

    internal sealed class Handler(IAuthDbContext context) : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            bool exists = await context.Users.AnyAsync(u => u.Email == request.Email, cancellationToken);
            if (exists)
            {
                return Result<int>.Failure(Error.Create("Email already registered."));
            }

            User user = new()
            {
                Id = 0,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                FullName = request.FullName,
                CreatedDate = DateTimeOffset.UtcNow,
                IsActive = true,
            };

            await context.Users.AddAsync(user, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(user.Id);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup(AuthConstants.Endpoints.AuthBase);

            group.MapPost("/register", async ([FromBody] Request request, ISender sender) =>
            {
                var result = await sender.Send(request);
                return result.IsSuccess
                    ? Results.Created($"{AuthConstants.Endpoints.AuthBase}/{result.Value}", result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<int>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .RequireRateLimiting("auth")
            .WithSummary("Register a new user.")
            .WithDescription("Creates a new user account and returns the user ID.")
            .WithTags(AuthConstants.Tags.Auth)
            .AllowAnonymous();
        }
    }
}
