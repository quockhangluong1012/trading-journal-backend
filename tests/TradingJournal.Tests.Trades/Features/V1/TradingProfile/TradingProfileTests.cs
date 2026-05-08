using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;
using TradingJournal.Shared.Abstractions;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Features.V1.TradingProfile;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Tests.Trades.Helpers;

namespace TradingJournal.Tests.Trades.Features.V1.TradingProfiles;

#region UpdateTradingProfile Validator

public sealed class UpdateTradingProfileValidatorTests
{
    private static readonly UpdateTradingProfile.Validator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_Valid()
    {
        var result = _validator.TestValidate(new UpdateTradingProfile.Request(5, 2.5m, 3, true));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Not_Have_Error_When_All_Nulls()
    {
        var result = _validator.TestValidate(new UpdateTradingProfile.Request(null, null, null, false));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_MaxTradesPerDay_Is_Negative()
    {
        var result = _validator.TestValidate(new UpdateTradingProfile.Request(-1, null, null, true));
        result.ShouldHaveValidationErrorFor(x => x.MaxTradesPerDay);
    }

    [Fact]
    public void Should_Have_Error_When_MaxDailyLossPercentage_Is_Negative()
    {
        var result = _validator.TestValidate(new UpdateTradingProfile.Request(null, -5m, null, true));
        result.ShouldHaveValidationErrorFor(x => x.MaxDailyLossPercentage);
    }

    [Fact]
    public void Should_Have_Error_When_MaxConsecutiveLosses_Is_Negative()
    {
        var result = _validator.TestValidate(new UpdateTradingProfile.Request(null, null, -2, true));
        result.ShouldHaveValidationErrorFor(x => x.MaxConsecutiveLosses);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Values_Are_Zero()
    {
        var result = _validator.TestValidate(new UpdateTradingProfile.Request(0, 0m, 0, false));
        result.ShouldNotHaveAnyValidationErrors();
    }
}

#endregion

#region GetTradingProfile Handler

public sealed class GetTradingProfileHandlerTests
{
    private readonly Mock<ITradeDbContext> _ctx = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();

    private GetTradingProfile.Handler CreateHandler() =>
        new(_ctx.Object, _httpContextAccessor.Object);

    private void SetupUserId(int userId)
    {
        var claims = new[] { new Claim("UserId", userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        _httpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);
    }

    [Fact]
    public async Task Handle_Returns_Default_When_No_Profile_Exists()
    {
        SetupUserId(42);
        _ctx.Setup(x => x.TradingProfiles)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<Modules.Trades.Domain.TradingProfile>().AsQueryable()).Object);

        var handler = CreateHandler();
        var result = await handler.Handle(new GetTradingProfile.Request(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Id);
        Assert.Null(result.Value.MaxTradesPerDay);
        Assert.False(result.Value.IsDisciplineEnabled);
    }

    [Fact]
    public async Task Handle_Returns_Profile_When_Exists()
    {
        SetupUserId(42);
        var profile = new Modules.Trades.Domain.TradingProfile
        {
            Id = 1,
            CreatedBy = 42,
            MaxTradesPerDay = 5,
            MaxDailyLossPercentage = 2.5m,
            MaxConsecutiveLosses = 3,
            IsDisciplineEnabled = true
        };
        _ctx.Setup(x => x.TradingProfiles)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<Modules.Trades.Domain.TradingProfile> { profile }.AsQueryable()).Object);

        var handler = CreateHandler();
        var result = await handler.Handle(new GetTradingProfile.Request(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Id);
        Assert.Equal(5, result.Value.MaxTradesPerDay);
        Assert.Equal(2.5m, result.Value.MaxDailyLossPercentage);
        Assert.Equal(3, result.Value.MaxConsecutiveLosses);
        Assert.True(result.Value.IsDisciplineEnabled);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_No_User()
    {
        _httpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        var handler = CreateHandler();
        var result = await handler.Handle(new GetTradingProfile.Request(), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}

#endregion

#region UpdateTradingProfile Handler

public sealed class UpdateTradingProfileHandlerTests
{
    private readonly Mock<ITradeDbContext> _ctx = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();

    private void SetupTransactionalExecution()
    {
        _ctx.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<Result<int>>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<Result<int>>> operation, CancellationToken ct) => operation(ct));
    }

    private UpdateTradingProfile.Handler CreateHandler() =>
        new(_ctx.Object, _httpContextAccessor.Object);

    private void SetupUserId(int userId)
    {
        var claims = new[] { new Claim("UserId", userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        _httpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);
    }

    [Fact]
    public async Task Handle_Creates_New_Profile_When_None_Exists()
    {
        SetupUserId(42);
        _ctx.Setup(x => x.TradingProfiles)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<Modules.Trades.Domain.TradingProfile>().AsQueryable()).Object);
        SetupTransactionalExecution();
        _ctx.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = CreateHandler();
        var result = await handler.Handle(new UpdateTradingProfile.Request(5, 2.5m, 3, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _ctx.Verify(x => x.TradingProfiles.AddAsync(It.IsAny<Modules.Trades.Domain.TradingProfile>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Updates_Existing_Profile()
    {
        SetupUserId(42);
        var existingProfile = new Modules.Trades.Domain.TradingProfile
        {
            Id = 1,
            CreatedBy = 42,
            MaxTradesPerDay = 3,
            MaxDailyLossPercentage = 1.0m,
            MaxConsecutiveLosses = 2,
            IsDisciplineEnabled = false
        };
        _ctx.Setup(x => x.TradingProfiles)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<Modules.Trades.Domain.TradingProfile> { existingProfile }.AsQueryable()).Object);
        SetupTransactionalExecution();
        _ctx.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = CreateHandler();
        var result = await handler.Handle(new UpdateTradingProfile.Request(10, 5.0m, 5, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, existingProfile.MaxTradesPerDay);
        Assert.Equal(5.0m, existingProfile.MaxDailyLossPercentage);
        Assert.Equal(5, existingProfile.MaxConsecutiveLosses);
        Assert.True(existingProfile.IsDisciplineEnabled);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_No_User()
    {
        _httpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        var handler = CreateHandler();
        var result = await handler.Handle(new UpdateTradingProfile.Request(5, 2.5m, 3, true), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}

#endregion
