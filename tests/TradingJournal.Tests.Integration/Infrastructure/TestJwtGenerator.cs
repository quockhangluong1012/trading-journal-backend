using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TradingJournal.Tests.Integration.Infrastructure;

/// <summary>
/// Generates fake JWT tokens for integration tests.
/// Uses the same secret/issuer/audience configured in TradingJournalWebFactory.
/// </summary>
public static class TestJwtGenerator
{
    private const string Secret = "IntegrationTestSecretKeyThatIs256BitsLong!!";
    private const string Issuer = "TradingJournal.Tests";
    private const string Audience = "TradingJournal.Tests";

    public static string GenerateToken(int userId = 1, string role = "User")
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(Secret));
        SigningCredentials credentials = new(key, SecurityAlgorithms.HmacSha256);

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, $"testuser{userId}"),
            new(ClaimTypes.Email, $"testuser{userId}@test.com"),
            new(ClaimTypes.Role, role),
        ];

        JwtSecurityToken token = new(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
