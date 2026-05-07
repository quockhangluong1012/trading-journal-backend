using FluentValidation.TestHelper;
using Moq;
using TradingJournal.Modules.Trades.Features.V1.TradingZone;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.ViewModel;
using TradingJournal.Shared.Interfaces;
using TradingJournal.Tests.Trades.Helpers;
using TradingZoneEntity = TradingJournal.Modules.Trades.Domain.TradingZone;

namespace TradingJournal.Tests.Trades.Features.V1.TradingZone;

public class CreateTradingZoneValidatorTests
{
    private static readonly CreateTradingZone.Validator _validator = new();
    [Fact] public void Should_Have_Error_When_Name_Is_Empty() { var r = _validator.TestValidate(new CreateTradingZone.Request("", "09:30", "16:00", null)); r.ShouldHaveValidationErrorFor(x => x.Name); }
    [Fact] public void Should_Have_Error_When_FromTime_Invalid() { var r = _validator.TestValidate(new CreateTradingZone.Request("Test", "invalid", "16:00", null)); r.ShouldHaveValidationErrorFor(x => x.FromTime); }
    [Fact] public void Should_Have_Error_When_ToTime_Invalid() { var r = _validator.TestValidate(new CreateTradingZone.Request("Test", "09:30", "invalid", null)); r.ShouldHaveValidationErrorFor(x => x.ToTime); }
    [Fact] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new CreateTradingZone.Request("Asia", "09:30", "16:00", "desc", 1)); r.ShouldNotHaveAnyValidationErrors(); }
}

public class CreateTradingZoneHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private CreateTradingZone.Handler _handler = null!;
    public CreateTradingZoneHandlerTests() { _dbMock = new Mock<ITradeDbContext>(); _handler = new CreateTradingZone.Handler(_dbMock.Object, new Mock<ICacheRepository>().Object); }
    [Fact] public async Task Handle_Returns_Failure_When_UserId_Is_Zero() { var result = await _handler.Handle(new CreateTradingZone.Request("Test", "09:30", "16:00", null, 0), CancellationToken.None); Assert.True(result.IsFailure); }
    [Fact] public async Task Handle_Returns_Success_When_Valid() { _dbMock.Setup(x => x.TradingZones).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradingZoneEntity>().AsQueryable()).Object); _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1); var result = await _handler.Handle(new CreateTradingZone.Request("Test", "09:30", "16:00", null, 1), CancellationToken.None); Assert.True(result.IsSuccess); }
}

public class GetTradingZonesHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private Mock<ICacheRepository> _cacheMock = null!;
    private GetTradingZones.Handler _handler = null!;
    public GetTradingZonesHandlerTests() { _dbMock = new Mock<ITradeDbContext>(); _cacheMock = new Mock<ICacheRepository>(); _cacheMock.Setup(x => x.GetOrCreateAsync<IReadOnlyCollection<TradingZoneViewModel>>(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task<IReadOnlyCollection<TradingZoneViewModel>>>>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>())).Returns<string, Func<CancellationToken, Task<IReadOnlyCollection<TradingZoneViewModel>>>, TimeSpan?, CancellationToken>(async (_, handle, _, ct) => (IReadOnlyCollection<TradingZoneViewModel>?)await handle(ct)); _handler = new GetTradingZones.Handler(_dbMock.Object, _cacheMock.Object); }
    [Fact] public async Task Handle_Returns_Zones_When_Data_Exists() { var zones = new List<TradingZoneEntity> { new() { Id = 1, Name = "Asia", CreatedBy = 1 } }.AsQueryable(); _dbMock.Setup(x => x.TradingZones).Returns(DbSetMockHelper.CreateMockDbSet(zones).Object); var result = await _handler.Handle(new GetTradingZones.Request(), CancellationToken.None); Assert.True(result.IsSuccess); Assert.NotEmpty(result.Value); }
    [Fact] public async Task Handle_Returns_Empty_When_No_Data() { _dbMock.Setup(x => x.TradingZones).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradingZoneEntity>().AsQueryable()).Object); var result = await _handler.Handle(new GetTradingZones.Request(), CancellationToken.None); Assert.True(result.IsSuccess); Assert.Empty(result.Value); }
}



public class GetTradingZoneDetailHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private GetTradingZoneDetail.Handler _handler = null!;
    public GetTradingZoneDetailHandlerTests() { _dbMock = new Mock<ITradeDbContext>(); _handler = new GetTradingZoneDetail.Handler(_dbMock.Object); }
    [Fact] public async Task Handle_Returns_Failure_When_Not_Found() { _dbMock.Setup(x => x.TradingZones).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradingZoneEntity>().AsQueryable()).Object); var result = await _handler.Handle(new GetTradingZoneDetail.Request(99, 1), CancellationToken.None); Assert.True(result.IsFailure); }
    [Fact] public async Task Handle_Returns_Failure_When_Zone_Belongs_To_Another_User() { var zone = new TradingZoneEntity { Id = 1, Name = "Asia", CreatedBy = 7, FromTime = "09:30", ToTime = "16:00" }; _dbMock.Setup(x => x.TradingZones).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradingZoneEntity> { zone }.AsQueryable()).Object); var result = await _handler.Handle(new GetTradingZoneDetail.Request(1, 1), CancellationToken.None); Assert.True(result.IsFailure); }
    [Fact] public async Task Handle_Returns_Success_When_Found() { var zone = new TradingZoneEntity { Id = 1, Name = "Asia", CreatedBy = 1, FromTime = "09:30", ToTime = "16:00" }; _dbMock.Setup(x => x.TradingZones).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradingZoneEntity> { zone }.AsQueryable()).Object); var result = await _handler.Handle(new GetTradingZoneDetail.Request(1, 1), CancellationToken.None); Assert.True(result.IsSuccess); Assert.Equal("Asia", result.Value.Name); }
}

public class UpdateTradingZoneValidatorTests
{
    private static readonly UpdateTradingZone.Validator _validator = new();
    [Fact] public void Should_Have_Error_When_Name_Is_Empty() { var r = _validator.TestValidate(new UpdateTradingZone.Request(1, "", "09:30", "16:00", "desc", 1)); r.ShouldHaveValidationErrorFor(x => x.Name); }
    [Fact] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new UpdateTradingZone.Request(1, "Asia", "09:30", "16:00", "desc", 1)); r.ShouldNotHaveAnyValidationErrors(); }
}

public class UpdateTradingZoneHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private UpdateTradingZone.Handler _handler = null!;
    public UpdateTradingZoneHandlerTests() { _dbMock = new Mock<ITradeDbContext>(); _handler = new UpdateTradingZone.Handler(_dbMock.Object, new Mock<ICacheRepository>().Object); }
    [Fact] public async Task Handle_Returns_Failure_When_Not_Found() { _dbMock.Setup(x => x.TradingZones).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradingZoneEntity>().AsQueryable()).Object); var result = await _handler.Handle(new UpdateTradingZone.Request(99, "Asia", "09:30", "16:00", "desc", 1), CancellationToken.None); Assert.True(result.IsFailure); }
    [Fact] public async Task Handle_Returns_Success_When_Updated() { var zone = new TradingZoneEntity { Id = 1, Name = "Old", CreatedBy = 1 }; _dbMock.Setup(x => x.TradingZones).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradingZoneEntity> { zone }.AsQueryable()).Object); _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1); var result = await _handler.Handle(new UpdateTradingZone.Request(1, "Asia", "09:30", "16:00", "desc", 1), CancellationToken.None); Assert.True(result.IsSuccess); }
}

public class DeleteTradingZoneValidatorTests
{
    private static readonly DeleteTradingZone.Validator _validator = new();
    [Fact] public void Should_Have_Error_When_Id_Is_Zero() { var r = _validator.TestValidate(new DeleteTradingZone.Request(0, 1)); r.ShouldHaveValidationErrorFor(x => x.Id); }
    [Fact] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new DeleteTradingZone.Request(1, 1)); r.ShouldNotHaveAnyValidationErrors(); }
}

public class DeleteTradingZoneHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private DeleteTradingZone.Handler _handler = null!;
    public DeleteTradingZoneHandlerTests() { _dbMock = new Mock<ITradeDbContext>(); _handler = new DeleteTradingZone.Handler(_dbMock.Object, new Mock<ICacheRepository>().Object); }
    [Fact] public async Task Handle_Returns_Failure_When_Not_Found() { _dbMock.Setup(x => x.TradingZones).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradingZoneEntity>().AsQueryable()).Object); var result = await _handler.Handle(new DeleteTradingZone.Request(99, 1), CancellationToken.None); Assert.True(result.IsFailure); }
    [Fact] public async Task Handle_Returns_Success_When_Deleted() { var zone = new TradingZoneEntity { Id = 1, Name = "Asia", CreatedBy = 1 }; _dbMock.Setup(x => x.TradingZones).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradingZoneEntity> { zone }.AsQueryable()).Object); _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1); var result = await _handler.Handle(new DeleteTradingZone.Request(1, 1), CancellationToken.None); Assert.True(result.IsSuccess); _dbMock.Verify(x => x.TradingZones.Remove(zone), Times.Once); }
}
