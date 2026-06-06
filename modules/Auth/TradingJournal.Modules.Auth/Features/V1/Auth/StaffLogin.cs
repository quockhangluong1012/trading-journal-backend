using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace TradingJournal.Modules.Auth.Features.V1.Auth;

public sealed class StaffLogin
{
    internal sealed record Request(string Email, string Password, bool RememberMe = false) : IQuery<Result<AuthResponse>>;

    internal sealed record AuthResponse(string Token, string RefreshToken, string Email, string FullName, DateTime Expiry, bool IsAdmin);

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
        // A precomputed hash to verify against when no staff matches, so a failed login takes the
        // same time whether or not the email exists — closing a user-enumeration timing side channel.
        private static readonly string DummyPasswordHash =
            BCrypt.Net.BCrypt.HashPassword("user-enumeration-timing-mitigation");

        public async Task<Result<AuthResponse>> Handle(Request request, CancellationToken cancellationToken)
        {
            Staff? staff = await context.Staffs.FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

            // Always run BCrypt.Verify (against a dummy hash when the staff is missing) so login
            // timing stays constant regardless of whether the email is registered.
            bool passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, staff?.PasswordHash ?? DummyPasswordHash);

            if (staff == null || !passwordValid)
            {
                return Result<AuthResponse>.Failure(Error.Create("Invalid admin email or password."));
            }

            if (!staff.IsActive)
            {
                return Result<AuthResponse>.Failure(Error.Create("Admin account is disabled."));
            }

            string token = GenerateJwtToken(staff, configuration);
            string refreshToken = RefreshToken.Handler.GenerateRefreshToken();
            DateTime expiry = DateTime.UtcNow.AddMinutes(TokenLifetime.AccessTokenMinutes(configuration));

            // Admin sessions follow the same model as users: short-lived access token, long-lived
            // rotating refresh token. This replaces the old 30-day admin access token (unrevocable).
            staff.RefreshToken = refreshToken;
            staff.RefreshTokenExpiry = DateTime.UtcNow.AddDays(TokenLifetime.RefreshTokenDays(configuration, request.RememberMe));
            await context.SaveChangesAsync(cancellationToken);

            return Result<AuthResponse>.Success(new AuthResponse(token, refreshToken, staff.Email, staff.FullName, expiry, staff.IsAdmin));
        }

        private static string GenerateJwtToken(Staff staff, IConfiguration configuration)
        {
            string secret = configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret is not configured.");
            string issuer = configuration["Jwt:Issuer"] ?? "TradingJournal";
            string audience = configuration["Jwt:Audience"] ?? "TradingJournal";
            int expiryMinutes = TokenLifetime.AccessTokenMinutes(configuration);

            SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(secret));
            SigningCredentials creds = new(key, SecurityAlgorithms.HmacSha256);

            // Only staff explicitly flagged as admins receive the "Admin" role claim, which
            // gates the AdminOnly authorization policy. Everyone else is a lower-privileged "Staff".
            string role = staff.IsAdmin ? "Admin" : "Staff";

            List<Claim> claims =
            [
                new(ClaimTypes.NameIdentifier, staff.Id.ToString()),
                new(ClaimTypes.Email, staff.Email),
                new(ClaimTypes.Name, staff.FullName),
                new(ClaimTypes.Role, role),
                new("UserId", staff.Id.ToString()),
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

            group.MapPost("/staff-login", async ([FromBody] Request request, ISender sender) =>
            {
                var result = await sender.Send(request);
                return result.IsSuccess
                    ? Results.Ok(result)
                    : Results.Unauthorized();
            })
            .Produces<Result<AuthResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireRateLimiting("auth")
            .WithSummary("Login and get JWT token for Staff.")
            .WithDescription("Authenticates a staff user and returns a JWT token.")
            .WithTags(AuthConstants.Tags.Auth)
            .AllowAnonymous();
        }
    }
}
