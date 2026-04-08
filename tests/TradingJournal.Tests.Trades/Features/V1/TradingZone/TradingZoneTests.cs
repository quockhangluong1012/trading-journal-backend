using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Moq;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Features.V1.TradingZone;
using TradingJournal.Modules.Trades.Infrastructure;

namespace TradingJournal.Tests.Trades.Features.V1.TradingZone;

[TestFixture]
public class CreateTradingZoneValidatorTests
{
    private static readonly CreateTradingZone.Validator _validator = new();
    [Test] public void Should_Have_Error_When_Name_Is_Empty() { var r = _validator.TestValidate(new CreateTradingZone.Request("", "09:30", "16:00", null)); r.ShouldHaveValidationErrorFor(x => x.Name); }
    [Test] public void Should_Have_Error_When_FromTime_Invalid() { var r = _validator.TestValidate(new CreateTradingZone.Request("Test", "invalid", "16:00", null)); r.ShouldHaveValidationErrorFor(x => x.FromTime); }
    [Test] public void Should_Have_Error_When_ToTime_Invalid() { var r = _validator.TestValidate(new CreateTradingZone.Request("Test", "09:30", "invalid", null)); r.ShouldHaveValidationErrorFor(x => x.ToTime); }
    [Test] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new CreateTradingZone.Request("Asia", "09:30", "16:00", "desc", 1)); r.ShouldNotHaveAnyErrors(); }
}

[TestFixture]
public class CreateTradingZoneHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private CreateTradingZone.Handler _handler = null!;
    [SetUp] public void SetUp() { _dbMock = new Mock<ITradeDbContext>(); _handler = new CreateTradingZone.Handler(_dbMock.Object); }
    [Test] public async Task Handle_Returns_Failure_When_UserId_Is_Zero() { var result = await _handler.Handle(new CreateTradingZone.Request("Test", "09:30", "16:00", null, 0), CancellationToken.None); result.IsFailure.Should().BeTrue(); }
    [Test] public async Task Handle_Returns_Success_When_Valid() { _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1); var result = await _handler.Handle(new CreateTradingZone.Request("Test", "09:30", "16:00", null, 1), CancellationToken.None); result.IsSuccess.Should().BeTrue(); }
}

[TestFixture]
public class GetTradingZonesHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private GetTradingZones.Handler _handler = null!;
    [SetUp] public void SetUp() { _dbMock = new Mock<ITradeDbContext>(); _handler = new GetTradingZones.Handler(_dbMock.Object); }
    [Test] public async Task Handle_Returns_Zones_When_Data_Exists() { var zones = new List<Domain.TradingZone> { new() { Id = 1, Name = "Asia", CreatedBy = 1 } }.AsQueryable(); _dbMock.Setup(x => x.TradingZones).Returns(zones); var result = await _handler.Handle(new GetTradingZones.Request(1), CancellationToken.None); result.IsSuccess.Should().BeTrue(); result.Value.Should().NotBeEmpty(); }
    [Test] public async Task Handle_Returns_Empty_When_No_Data() { _dbMock.Setup(x => x.TradingZones).Returns(new List<Domain.TradingZone>().AsQueryable()); var result = await _handler.Handle(new GetTradingZones.Request(1), CancellationToken.None); result.IsSuccess.Should().BeTrue(); result.Value.Should().BeEmpty(); }
}



[TestFixture]
public class GetTradingZoneDetailHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private GetTradingZoneDetail.Handler _handler = null!;
    [SetUp] public void SetUp() { _dbMock = new Mock<ITradeDbContext>(); _handler = new GetTradingZoneDetail.Handler(_dbMock.Object); }
    [Test] public async Task Handle_Returns_Failure_When_Not_Found() { _dbMock.Setup(x => x.TradingZones.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((Domain.TradingZone?)null); var result = await _handler.Handle(new GetTradingZoneDetail.Request(99, 1), CancellationToken.None); result.IsFailure.Should().BeTrue(); }
    [Test] public async Task Handle_Returns_Success_When_Found() { var zone = new Domain.TradingZone { Id = 1, Name = "Asia", CreatedBy = 1, FromTime = "09:30", ToTime = "16:00" }; _dbMock.Setup(x => x.TradingZones.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(zone); var result = await _handler.Handle(new GetTradingZoneDetail.Request(1, 1), CancellationToken.None); result.IsSuccess.Should().BeTrue(); result.Value.Name.Should().Be("Asia"); }
}

[TestFixture]
public class UpdateTradingZoneValidatorTests
{
    private static readonly UpdateTradingZone.Validator _validator = new();
    [Test] public void Should_Have_Error_When_Name_Is_Empty() { var r = _validator.TestValidate(new UpdateTradingZone.Request("", "09:30", "16:00", "desc", 1, 1)); r.ShouldHaveValidationErrorFor(x => x.Name); }
    [Test] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new UpdateTradingZone.Request("Asia", "09:30", "16:00", "desc", 1, 1)); r.ShouldNotHaveAnyErrors(); }
}

[TestFixture]
public class UpdateTradingZoneHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private UpdateTradingZone.Handler _handler = null!;
    [SetUp] public void SetUp() { _dbMock = new Mock<ITradeDbContext>(); _handler = new UpdateTradingZone.Handler(_dbMock.Object); }
    [Test] public async Task Handle_Returns_Failure_When_Not_Found() { _dbMock.Setup(x => x.TradingZones.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((Domain.TradingZone?)null); var result = await _handler.Handle(new UpdateTradingZone.Request("Asia", "09:30", "16:00", "desc", 99, 1), CancellationToken.None); result.IsFailure.Should().BeTrue(); }
    [Test] public async Task Handle_Returns_Success_When_Updated() { var zone = new Domain.TradingZone { Id = 1, Name = "Old", CreatedBy = 1 }; _dbMock.Setup(x => x.TradingZones.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(zone); _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1); var result = await _handler.Handle(new UpdateTradingZone.Request("Asia", "09:30", "16:00", "desc", 1, 1), CancellationToken.None); result.IsSuccess.Should().BeTrue(); }
}

[TestFixture]
public class DeleteTradingZoneValidatorTests
{
    private static readonly DeleteTradingZone.Validator _validator = new();
    [Test] public void Should_Have_Error_When_Id_Is_Zero() { var r = _validator.TestValidate(new DeleteTradingZone.Request(0, 1)); r.ShouldHaveValidationErrorFor(x => x.Id); }
    [Test] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new DeleteTradingZone.Request(1, 1)); r.ShouldNotHaveAnyErrors(); }
}

[TestFixture]
public class DeleteTradingZoneHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private DeleteTradingZone.Handler _handler = null!;
    [SetUp] public void SetUp() { _dbMock = new Mock<ITradeDbContext>(); _handler = new DeleteTradingZone.Handler(_dbMock.Object); }
    [Test] public async Task Handle_Returns_Failure_When_Not_Found() { _dbMock.Setup(x => x.TradingZones.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((Domain.TradingZone?)null); var result = await _handler.Handle(new DeleteTradingZone.Request(99, 1), CancellationToken.None); result.IsFailure.Should().BeTrue(); }
    [Test] public async Task Handle_Returns_Success_When_Deleted() { var zone = new Domain.TradingZone { Id = 1, Name = "Asia", CreatedBy = 1 }; _dbMock.Setup(x => x.TradingZones.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(zone); _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1); var result = await _handler.Handle(new DeleteTradingZone.Request(1, 1), CancellationToken.None); result.IsSuccess.Should().BeTrue(); _dbMock.Verify(x => x.TradingZones.Remove(zone), Times.Once); }
}
