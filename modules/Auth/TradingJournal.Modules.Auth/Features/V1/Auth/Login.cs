using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace TradingJournal.Modules.Auth.Features.V1.Auth;

public sealed class Login
{
    internal sealed record Request(string Email, string Password, bool RememberMe = false) : IQuery<Result<AuthResponse>>;

    internal sealed record AuthResponse(string Token, string RefreshToken, string Email, string FullName, DateTime Expiry);

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
        // A precomputed hash to verify against when no user matches, so a failed login takes the
        // same time whether or not the email exists — closing a user-enumeration timing side channel.
        private static readonly string DummyPasswordHash =
            BCrypt.Net.BCrypt.HashPassword("user-enumeration-timing-mitigation");

        public async Task<Result<AuthResponse>> Handle(Request request, CancellationToken cancellationToken)
        {
            User? user = await context.Users.FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

            // Always run BCrypt.Verify (against a dummy hash when the user is missing) so login
            // timing stays constant regardless of whether the email is registered.
            bool passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, user?.PasswordHash ?? DummyPasswordHash);

            if (user == null || !passwordValid)
            {
                return Result<AuthResponse>.Failure(Error.Create("Invalid email or password."));
            }

            if (!user.IsActive)
            {
                return Result<AuthResponse>.Failure(Error.Create("Account is disabled."));
            }

            string token = GenerateJwtToken(user, configuration);
            string refreshToken = RefreshToken.Handler.GenerateRefreshToken();
            DateTime expiry = DateTime.UtcNow.AddMinutes(TokenLifetime.AccessTokenMinutes(configuration));

            // The access token is short-lived; long sessions ("remember me") are sustained by a
            // longer-lived refresh token that is rotated on every use.
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(TokenLifetime.RefreshTokenDays(configuration, request.RememberMe));
            await context.SaveChangesAsync(cancellationToken);

            return Result<AuthResponse>.Success(new AuthResponse(token, refreshToken, user.Email, user.FullName, expiry));
        }

        private static string GenerateJwtToken(User user, IConfiguration configuration)
        {
            string secret = configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret is not configured.");
            string issuer = configuration["Jwt:Issuer"] ?? "TradingJournal";
            string audience = configuration["Jwt:Audience"] ?? "TradingJournal";
            int expiryMinutes = TokenLifetime.AccessTokenMinutes(configuration);

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
            .RequireRateLimiting("auth")
            .WithSummary("Login and get JWT token.")
            .WithDescription("Authenticates a user and returns a JWT token and refresh token for subsequent API calls.")
            .WithTags(AuthConstants.Tags.Auth)
            .AllowAnonymous();
        }
    }
}

