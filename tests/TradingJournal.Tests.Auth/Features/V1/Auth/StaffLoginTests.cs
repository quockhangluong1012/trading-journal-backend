using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Configuration;
using Moq;
using TradingJournal.Modules.Auth;
using TradingJournal.Modules.Auth.Features.V1.Auth;
using TradingJournal.Modules.Auth.Infrastructure;

namespace TradingJournal.Tests.Auth.Features.V1.Auth;

public class StaffLoginValidatorTests
{
    private static readonly StaffLogin.Validator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Email_Is_Empty()
    {
        var result = _validator.TestValidate(new StaffLogin.Request("", "password123"));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Should_Have_Error_When_Password_Is_Empty()
    {
        var result = _validator.TestValidate(new StaffLogin.Request("admin@example.com", ""));
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Both_Are_Filled()
    {
        var result = _validator.TestValidate(new StaffLogin.Request("admin@example.com", "password123"));
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class StaffLoginHandlerTests
{
    private readonly Mock<IAuthDbContext> _contextMock = new();
    private readonly StaffLogin.Handler _handler;

    public StaffLoginHandlerTests()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "a-very-long-secret-key-that-is-at-least-32-chars!",
                ["Jwt:Issuer"] = "TradingJournal",
                ["Jwt:Audience"] = "TradingJournal",
                ["Jwt:ExpiryMinutes"] = "60",
            })
            .Build();
        _handler = new StaffLogin.Handler(_contextMock.Object, configuration);
    }

    private void SetupStaff(params Staff[] staffs)
    {
        _contextMock.Setup(x => x.Staffs).Returns(staffs.ToList().BuildMockDbSet().Object);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    private static Staff ActiveStaff() => new()
    {
        Id = 1,
        Email = "admin@example.com",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
        FullName = "Admin User",
        IsActive = true,
        IsAdmin = true,
    };

    private static Staff NonAdminStaff() => new()
    {
        Id = 2,
        Email = "staff@example.com",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
        FullName = "Regular Staff",
        IsActive = true,
        IsAdmin = false,
    };

    [Fact]
    public async Task Handle_Returns_Token_And_RefreshToken_On_Valid_Credentials()
    {
        Staff staff = ActiveStaff();
        SetupStaff(staff);

        var result = await _handler.Handle(new StaffLogin.Request("admin@example.com", "password123"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsAdmin);
        Assert.False(string.IsNullOrEmpty(result.Value.Token));
        Assert.False(string.IsNullOrEmpty(result.Value.RefreshToken));
        // The refresh token must be persisted so it can be rotated later.
        Assert.Equal(result.Value.RefreshToken, staff.RefreshToken);
        Assert.NotNull(staff.RefreshTokenExpiry);
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Grants_Admin_Role_Only_To_Admin_Staff()
    {
        SetupStaff(ActiveStaff());

        var result = await _handler.Handle(new StaffLogin.Request("admin@example.com", "password123"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsAdmin);
        Assert.Equal("Admin", ReadRoleClaim(result.Value.Token));
    }

    [Fact]
    public async Task Handle_Does_Not_Grant_Admin_To_NonAdmin_Staff()
    {
        SetupStaff(NonAdminStaff());

        var result = await _handler.Handle(new StaffLogin.Request("staff@example.com", "password123"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // A non-admin staff must never receive admin privileges or the Admin role claim.
        Assert.False(result.Value.IsAdmin);
        Assert.Equal("Staff", ReadRoleClaim(result.Value.Token));
    }

    private static string? ReadRoleClaim(string token)
    {
        JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        return jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
    }

    [Fact]
    public async Task Handle_Caps_Access_Token_Expiry_Even_With_RememberMe()
    {
        SetupStaff(ActiveStaff());

        var result = await _handler.Handle(new StaffLogin.Request("admin@example.com", "password123", RememberMe: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Access token must never be a long-lived (e.g. 30-day) token — capped at <= 60 min.
        Assert.True(result.Value.Expiry <= DateTime.UtcNow.AddMinutes(TokenLifetime.MaxAccessTokenMinutes).AddSeconds(5));
    }

    [Fact]
    public async Task Handle_Fails_On_Wrong_Password()
    {
        SetupStaff(ActiveStaff());

        var result = await _handler.Handle(new StaffLogin.Request("admin@example.com", "wrong"), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Fails_When_Staff_Disabled()
    {
        Staff staff = ActiveStaff();
        staff.IsActive = false;
        SetupStaff(staff);

        var result = await _handler.Handle(new StaffLogin.Request("admin@example.com", "password123"), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Fails_When_Staff_Not_Found()
    {
        SetupStaff();

        var result = await _handler.Handle(new StaffLogin.Request("nobody@example.com", "password123"), CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
