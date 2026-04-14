using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace TradingJournal.Modules.Auth.Features.V1.Auth;

public sealed class Login
{
    internal sealed record Request(string Email, string Password, bool RememberMe = false) : IQuery<Result<AuthResponse>>;

    internal sealed record AuthResponse(string Token, string Email, string FullName, DateTime Expiry);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Email)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Email is required.");

            RuleFor(x => x.Password)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Password is required.");
        }
    }

    internal sealed class Handler(IAuthDbContext context, IConfiguration configuration)
        : IQueryHandler<Request, Result<AuthResponse>>
    {
        public async Task<Result<AuthResponse>> Handle(Request request, CancellationToken cancellationToken)
        {
            User? user = await context.Users.FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Result<AuthResponse>.Failure(Error.Create("Invalid email or password."));
            }

            if (!user.IsActive)
            {
                return Result<AuthResponse>.Failure(Error.Create("Account is disabled."));
            }

            string token = GenerateJwtToken(user, configuration, request.RememberMe);
            int expiryMinutes = request.RememberMe ? 30 * 24 * 60 : configuration.GetValue<int>("Jwt:ExpiryMinutes", 60);
            DateTime expiry = DateTime.UtcNow.AddMinutes(expiryMinutes);

            return Result<AuthResponse>.Success(new AuthResponse(token, user.Email, user.FullName, expiry));
        }

        private static string GenerateJwtToken(User user, IConfiguration configuration, bool rememberMe)
        {
            string secret = configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret is not configured.");
            string issuer = configuration["Jwt:Issuer"] ?? "TradingJournal";
            string audience = configuration["Jwt:Audience"] ?? "TradingJournal";
            int expiryMinutes = rememberMe ? 30 * 24 * 60 : configuration.GetValue<int>("Jwt:ExpiryMinutes", 60);

            SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(secret));
            SigningCredentials creds = new(key, SecurityAlgorithms.HmacSha256);

            List<Claim> claims =
            [
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Name, user.FullName),
                new("UserId", user.Id.ToString()),
            ];

            JwtSecurityToken token = new(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup(AuthConstants.Endpoints.AuthBase);

            group.MapPost("/login", async ([FromBody] Request request, ISender sender) =>
            {
                var result = await sender.Send(request);
                return result.IsSuccess
                    ? Results.Ok(result)
                    : Results.Unauthorized();
            })
            .Produces<Result<AuthResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithSummary("Login and get JWT token.")
            .WithDescription("Authenticates a user and returns a JWT token for subsequent API calls.")
            .WithTags(AuthConstants.Tags.Auth)
            .AllowAnonymous();
        }
    }
}
