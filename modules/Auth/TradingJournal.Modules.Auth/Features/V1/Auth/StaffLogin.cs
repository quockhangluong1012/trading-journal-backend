using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace TradingJournal.Modules.Auth.Features.V1.Auth;

public sealed class StaffLogin
{
    internal sealed record Request(string Email, string Password, bool RememberMe = false) : IQuery<Result<AuthResponse>>;

    internal sealed record AuthResponse(string Token, string Email, string FullName, DateTime Expiry, bool IsAdmin);

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
            Staff? staff = await context.Staffs.FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

            if (staff == null || !BCrypt.Net.BCrypt.Verify(request.Password, staff.PasswordHash))
            {
                return Result<AuthResponse>.Failure(Error.Create("Invalid admin email or password."));
            }

            if (!staff.IsActive)
            {
                return Result<AuthResponse>.Failure(Error.Create("Admin account is disabled."));
            }

            string token = GenerateJwtToken(staff, configuration, request.RememberMe);
            int expiryMinutes = request.RememberMe ? 30 * 24 * 60 : configuration.GetValue<int>("Jwt:ExpiryMinutes", 60);
            DateTime expiry = DateTime.UtcNow.AddMinutes(expiryMinutes);

            return Result<AuthResponse>.Success(new AuthResponse(token, staff.Email, staff.FullName, expiry, true));
        }

        private static string GenerateJwtToken(Staff staff, IConfiguration configuration, bool rememberMe)
        {
            string secret = configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret is not configured.");
            string issuer = configuration["Jwt:Issuer"] ?? "TradingJournal";
            string audience = configuration["Jwt:Audience"] ?? "TradingJournal";
            int expiryMinutes = rememberMe ? 30 * 24 * 60 : configuration.GetValue<int>("Jwt:ExpiryMinutes", 60);

            SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(secret));
            SigningCredentials creds = new(key, SecurityAlgorithms.HmacSha256);

            List<Claim> claims =
            [
                new(ClaimTypes.NameIdentifier, staff.Id.ToString()),
                new(ClaimTypes.Email, staff.Email),
                new(ClaimTypes.Name, staff.FullName),
                new(ClaimTypes.Role, "Admin"),
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
            .WithSummary("Login and get JWT token for Staff.")
            .WithDescription("Authenticates a staff user and returns a JWT token.")
            .WithTags(AuthConstants.Tags.Auth)
            .AllowAnonymous();
        }
    }
}
