using FluentValidation.TestHelper;
using Microsoft.Extensions.Configuration;
using Moq;
using TradingJournal.Modules.Auth;
using TradingJournal.Modules.Auth.Features.V1.Auth;
using TradingJournal.Modules.Auth.Infrastructure;

namespace TradingJournal.Tests.Auth.Features.V1.Auth;

public class LoginValidatorTests
{
    private static readonly Login.Validator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Email_Is_Empty()
    {
        var request = new Login.Request("", "password123");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Should_Have_Error_When_Password_Is_Empty()
    {
        var request = new Login.Request("test@example.com", "");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Both_Are_Filled()
    {
        var request = new Login.Request("test@example.com", "password123");
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }
}

public class LoginHandlerTests
{
    private Mock<IAuthDbContext> _contextMock = null!;
    private Login.Handler _handler = null!;

    public LoginHandlerTests()
    {
        _contextMock = new Mock<IAuthDbContext>();
        var inMemorySettings = new System.Collections.Generic.Dictionary<string, string?> {
            {"Jwt:Secret", "a-very-long-secret-key-that-is-at-least-32-chars!"},
            {"Jwt:Issuer", "TradingJournal"},
            {"Jwt:Audience", "TradingJournal"},
            {"Jwt:ExpiryMinutes", "60"}
        };
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
        _handler = new Login.Handler(_contextMock.Object, configuration);
    }

    [Fact]
    public async Task Handle_Returns_Success_When_Credentials_Are_Valid()
    {
        // Arrange
        var user = new User { Id = 1, Email = "test@example.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"), FullName = "Test User", IsActive = true };
        var users = new System.Collections.Generic.List<User> { user };
        var userSet = users.BuildMockDbSet();
        _contextMock.Setup(x => x.Users).Returns(userSet.Object);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var request = new Login.Request("test@example.com", "password123");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("test@example.com", result.Value.Email);
        Assert.Equal("Test User", result.Value.FullName);
        Assert.NotNull(result.Value.Token); Assert.NotEmpty(result.Value.Token);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_User_Not_Found()
    {
        // Arrange
        // Arrange
        var user = new User { Id = 1, Email = "test@example.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"), FullName = "Test User", IsActive = true };
        var users = new System.Collections.Generic.List<User> { user };
        var userSet = users.BuildMockDbSet();
        _contextMock.Setup(x => x.Users).Returns(userSet.Object);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var request = new Login.Request("nonexistent@example.com", "password123");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Password_Is_Wrong()
    {
        // Arrange
        // Arrange
        var user = new User { Id = 1, Email = "test@example.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"), FullName = "Test User", IsActive = true };
        var users = new System.Collections.Generic.List<User> { user };
        var userSet = users.BuildMockDbSet();
        _contextMock.Setup(x => x.Users).Returns(userSet.Object);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var request = new Login.Request("test@example.com", "wrongpassword");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Account_Is_Disabled()
    {
        // Arrange
        // Arrange
        var user = new User { Id = 1, Email = "test@example.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"), FullName = "Test User", IsActive = false };
        var users = new System.Collections.Generic.List<User> { user };
        var userSet = users.BuildMockDbSet();
        _contextMock.Setup(x => x.Users).Returns(userSet.Object);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var request = new Login.Request("test@example.com", "password123");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
    }
}
