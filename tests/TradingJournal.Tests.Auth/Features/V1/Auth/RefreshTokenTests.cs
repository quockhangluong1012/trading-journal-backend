using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Moq;
using TradingJournal.Modules.Auth;
using TradingJournal.Modules.Auth.Features.V1.Auth;
using TradingJournal.Modules.Auth.Infrastructure;

namespace TradingJournal.Tests.Auth.Features.V1.Auth;

public class RefreshTokenHandlerTests
{
    private const string Secret = "a-very-long-secret-key-that-is-at-least-32-chars!";
    private const string Issuer = "TradingJournal";
    private const string Audience = "TradingJournal";

    private readonly Mock<IAuthDbContext> _contextMock = new();
    private readonly IConfiguration _configuration;
    private readonly RefreshToken.Handler _handler;

    public RefreshTokenHandlerTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = Secret,
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                ["Jwt:ExpiryMinutes"] = "60",
            })
            .Build();
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _handler = new RefreshToken.Handler(_contextMock.Object, _configuration);
    }

    /// <summary>Builds a signed (already-expired) access token the handler will accept for refresh.</summary>
    private static string BuildAccessToken(int userId, bool admin)
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(Secret));
        SigningCredentials creds = new(key, SecurityAlgorithms.HmacSha256);

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("UserId", userId.ToString()),
        ];
        if (admin)
        {
            claims.Add(new(ClaimTypes.Role, "Admin"));
        }

        JwtSecurityToken token = new(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(-5),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task Handle_Rotates_Refresh_Token_For_User()
    {
        var user = new User
        {
            Id = 7,
            Email = "user@example.com",
            FullName = "A User",
            PasswordHash = "x",
            IsActive = true,
            RefreshToken = "old-refresh",
            RefreshTokenExpiry = DateTime.UtcNow.AddDays(3),
        };
        _contextMock.Setup(x => x.Users).Returns(new List<User> { user }.BuildMockDbSet().Object);

        var request = new RefreshToken.Request(BuildAccessToken(7, admin: false), "old-refresh");
        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrEmpty(result.Value.Token));
        Assert.False(string.IsNullOrEmpty(result.Value.RefreshToken));
        Assert.NotEqual("old-refresh", result.Value.RefreshToken);
        Assert.Equal(result.Value.RefreshToken, user.RefreshToken); // rotated + persisted
    }

    [Fact]
    public async Task Handle_Rotates_Refresh_Token_For_Staff()
    {
        var staff = new Staff
        {
            Id = 3,
            Email = "admin@example.com",
            FullName = "Admin",
            PasswordHash = "x",
            IsActive = true,
            RefreshToken = "staff-refresh",
            RefreshTokenExpiry = DateTime.UtcNow.AddDays(3),
        };
        _contextMock.Setup(x => x.Staffs).Returns(new List<Staff> { staff }.BuildMockDbSet().Object);

        var request = new RefreshToken.Request(BuildAccessToken(3, admin: true), "staff-refresh");
        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual("staff-refresh", result.Value.RefreshToken);
        Assert.Equal(result.Value.RefreshToken, staff.RefreshToken);

        // The reissued staff access token must still carry the Admin role claim.
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Value.Token);
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Admin");
    }

    [Fact]
    public async Task Handle_Fails_When_Refresh_Token_Does_Not_Match()
    {
        var user = new User
        {
            Id = 7,
            Email = "user@example.com",
            FullName = "A User",
            PasswordHash = "x",
            IsActive = true,
            RefreshToken = "the-real-one",
            RefreshTokenExpiry = DateTime.UtcNow.AddDays(3),
        };
        _contextMock.Setup(x => x.Users).Returns(new List<User> { user }.BuildMockDbSet().Object);

        var request = new RefreshToken.Request(BuildAccessToken(7, admin: false), "wrong-refresh");
        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Fails_When_Refresh_Token_Expired()
    {
        var user = new User
        {
            Id = 7,
            Email = "user@example.com",
            FullName = "A User",
            PasswordHash = "x",
            IsActive = true,
            RefreshToken = "rt",
            RefreshTokenExpiry = DateTime.UtcNow.AddMinutes(-1),
        };
        _contextMock.Setup(x => x.Users).Returns(new List<User> { user }.BuildMockDbSet().Object);

        var request = new RefreshToken.Request(BuildAccessToken(7, admin: false), "rt");
        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Fails_On_Garbage_Access_Token()
    {
        var request = new RefreshToken.Request("not-a-jwt", "rt");
        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
