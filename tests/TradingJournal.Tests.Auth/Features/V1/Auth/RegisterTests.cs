using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Moq;
using TradingJournal.Modules.Auth;
using TradingJournal.Modules.Auth.Features.V1.Auth;
using TradingJournal.Modules.Auth.Infrastructure;

namespace TradingJournal.Tests.Auth.Features.V1.Auth;

[TestFixture]
public class RegisterValidatorTests
{
    private static readonly Register.Validator _validator = new();

    [Test]
    public void Should_Have_Error_When_Email_Is_Empty()
    {
        var request = new Register.Request("", "password123", "Test User");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Test]
    public void Should_Have_Error_When_Email_Is_Invalid()
    {
        var request = new Register.Request("not-an-email", "password123", "Test User");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Test]
    public void Should_Have_Error_When_Password_Is_Short()
    {
        var request = new Register.Request("test@example.com", "short", "Test User");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Test]
    public void Should_Have_Error_When_Password_Is_Empty()
    {
        var request = new Register.Request("test@example.com", "", "Test User");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Test]
    public void Should_Have_Error_When_FullName_Is_Empty()
    {
        var request = new Register.Request("test@example.com", "password123", "");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.FullName);
    }

    [Test]
    public void Should_Not_Have_Error_When_All_Fields_Are_Valid()
    {
        var request = new Register.Request("test@example.com", "password123", "Test User");
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
        result.ShouldNotHaveValidationErrorFor(x => x.FullName);
    }
}

[TestFixture]
public class RegisterHandlerTests
{
    private Mock<IAuthDbContext> _contextMock = null!;
    private Register.Handler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _contextMock = new Mock<IAuthDbContext>();
        _handler = new Register.Handler(_contextMock.Object);
    }

    [Test]
    public async Task Handle_Returns_Success_When_Email_Is_New()
    {
        // Arrange
        // Arrange
        var user = new User { Id = 1, Email = "test@example.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"), FullName = "Test User", IsActive = true };
        var users = new System.Collections.Generic.List<User> { user };
        var userSet = users.BuildMockDbSet();
        _contextMock.Setup(x => x.Users).Returns(userSet.Object);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var request = new Register.Request("new@example.com", "password123", "New User");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        _contextMock.Verify(x => x.Users.AddAsync(It.Is<User>(u => u.Email == "new@example.com"), It.IsAny<CancellationToken>()), Times.Once);
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_Returns_Failure_When_Email_Already_Exists()
    {
        // Arrange
        // Arrange
        var user = new User { Id = 1, Email = "existing@example.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"), FullName = "Test User", IsActive = true };
        var users = new System.Collections.Generic.List<User> { user };
        var userSet = users.BuildMockDbSet();
        _contextMock.Setup(x => x.Users).Returns(userSet.Object);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var request = new Register.Request("existing@example.com", "password123", "Dup User");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.That(result.IsFailure, Is.True);
        _contextMock.Verify(x => x.Users.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
