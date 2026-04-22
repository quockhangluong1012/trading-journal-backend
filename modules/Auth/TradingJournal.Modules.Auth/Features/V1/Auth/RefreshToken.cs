using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace TradingJournal.Modules.Auth.Features.V1.Auth;

public sealed class RefreshToken
{
    internal sealed record Request(string AccessToken, string RefreshToken) : IQuery<Result<AuthResponse>>;

    internal sealed record AuthResponse(string Token, string RefreshToken, string Email, string FullName, DateTime Expiry);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.AccessToken)
                .NotEmpty().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Access token is required.");

            RuleFor(x => x.RefreshToken)
                .NotEmpty().WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Refresh token is required.");
        }
    }

    internal sealed class Handler(IAuthDbContext context, IConfiguration configuration)
        : IQueryHandler<Request, Result<AuthResponse>>
    {
        public async Task<Result<AuthResponse>> Handle(Request request, CancellationToken cancellationToken)
        {
            // Extract the user identity from the expired access token (without validating lifetime)
            ClaimsPrincipal? principal = GetPrincipalFromExpiredToken(request.AccessToken, configuration);

            if (principal == null)
            {
                return Result<AuthResponse>.Failure(Error.Create("Invalid access token."));
            }

            string? userIdClaim = principal.FindFirst("UserId")?.Value
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Result<AuthResponse>.Failure(Error.Create("Invalid token claims."));
            }

            User? user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (user == null || !user.IsActive)
            {
                return Result<AuthResponse>.Failure(Error.Create("User not found or disabled."));
            }

            if (user.RefreshToken != request.RefreshToken || 
                user.RefreshTokenExpiry == null || 
                user.RefreshTokenExpiry <= DateTime.UtcNow)
            {
                return Result<AuthResponse>.Failure(Error.Create("Invalid or expired refresh token."));
            }

            // Generate new tokens
            string newAccessToken = GenerateJwtToken(user, configuration);
            string newRefreshToken = GenerateRefreshToken();
            int expiryMinutes = configuration.GetValue<int>("Jwt:ExpiryMinutes", 60);
            DateTime expiry = DateTime.UtcNow.AddMinutes(expiryMinutes);

            // Rotate refresh token (invalidates the old one)
            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);

            await context.SaveChangesAsync(cancellationToken);

            return Result<AuthResponse>.Success(new AuthResponse(
                newAccessToken, newRefreshToken, user.Email, user.FullName, expiry));
        }

        private static ClaimsPrincipal? GetPrincipalFromExpiredToken(string token, IConfiguration configuration)
        {
            string secret = configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret is not configured.");

            TokenValidationParameters validationParameters = new()
            {
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                ValidIssuer = configuration["Jwt:Issuer"] ?? "TradingJournal",
                ValidAudience = configuration["Jwt:Audience"] ?? "TradingJournal",
                ValidateLifetime = false // Allow expired tokens for refresh
            };

            try
            {
                ClaimsPrincipal principal = new JwtSecurityTokenHandler()
                    .ValidateToken(token, validationParameters, out SecurityToken securityToken);

                if (securityToken is not JwtSecurityToken jwtToken ||
                    !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    return null;
                }

                return principal;
            }
            catch
            {
                return null;
            }
        }

        private static string GenerateJwtToken(User user, IConfiguration configuration)
        {
            string secret = configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret is not configured.");
            string issuer = configuration["Jwt:Issuer"] ?? "TradingJournal";
            string audience = configuration["Jwt:Audience"] ?? "TradingJournal";
            int expiryMinutes = configuration.GetValue<int>("Jwt:ExpiryMinutes", 60);

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

        internal static string GenerateRefreshToken()
        {
            byte[] randomNumber = new byte[64];
            using RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup(AuthConstants.Endpoints.AuthBase);

            group.MapPost("/refresh", async ([FromBody] Request request, ISender sender) =>
            {
                var result = await sender.Send(request);
                return result.IsSuccess
                    ? Results.Ok(result)
                    : Results.Unauthorized();
            })
            .Produces<Result<AuthResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireRateLimiting("auth")
            .WithSummary("Refresh access token.")
            .WithDescription("Uses a valid refresh token to issue a new access token and rotate the refresh token.")
            .WithTags(AuthConstants.Tags.Auth)
            .AllowAnonymous();
        }
    }
}
