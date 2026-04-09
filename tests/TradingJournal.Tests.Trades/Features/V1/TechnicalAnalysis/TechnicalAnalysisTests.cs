using FluentValidation.TestHelper;
using Moq;
using TradingJournal.Modules.Trades.Features.V1.TechnicalAnalysis;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Tests.Trades.Helpers;
using TechnicalAnalysisEntity = TradingJournal.Modules.Trades.Domain.TechnicalAnalysis;

namespace TradingJournal.Tests.Trades.Features.V1.TechnicalAnalysis;

public class CreateTechnicalAnalysisValidatorTests
{
    private static readonly CreateTechnicalAnalysis.Validator _validator = new();
    [Fact] public void Should_Have_Error_When_Name_Is_Null() { var r = _validator.TestValidate(new CreateTechnicalAnalysis.Request(null!, "SMA", "desc")); r.ShouldHaveValidationErrorFor(x => x.Name); }
    [Fact] public void Should_Have_Error_When_Name_Is_Empty() { var r = _validator.TestValidate(new CreateTechnicalAnalysis.Request("", "SMA", "desc")); r.ShouldHaveValidationErrorFor(x => x.Name); }
    [Fact] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new CreateTechnicalAnalysis.Request("SMA", "SMA", "Simple Moving Average")); r.ShouldNotHaveAnyValidationErrors(); }
}

public class CreateTechnicalAnalysisHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private CreateTechnicalAnalysis.Handler _handler = null!;
    public CreateTechnicalAnalysisHandlerTests() { _dbMock = new Mock<ITradeDbContext>(); _handler = new CreateTechnicalAnalysis.Handler(_dbMock.Object); }
    [Fact] public async Task Handle_Returns_Failure_When_UserId_Is_Zero() { var result = await _handler.Handle(new CreateTechnicalAnalysis.Request("Test", "T", "desc", 0), CancellationToken.None); Assert.True(result.IsFailure); }
    [Fact] public async Task Handle_Returns_Success_When_Valid() { _dbMock.Setup(x => x.TechnicalAnalyses).Returns(DbSetMockHelper.CreateMockDbSet(new List<TechnicalAnalysisEntity>().AsQueryable()).Object); _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1); var result = await _handler.Handle(new CreateTechnicalAnalysis.Request("Test", "T", "desc", 1), CancellationToken.None); Assert.True(result.IsSuccess); }
}

public class GetTechnicalAnalysisValidatorTests
{
    private static readonly GetTechnicalAnalysis.Validator _validator = new();
    [Fact] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new GetTechnicalAnalysis.Request()); r.ShouldNotHaveAnyValidationErrors(); }
}

public class GetTechnicalAnalysisHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private GetTechnicalAnalysis.Handler _handler = null!;
    public GetTechnicalAnalysisHandlerTests() { _dbMock = new Mock<ITradeDbContext>(); _handler = new GetTechnicalAnalysis.Handler(_dbMock.Object); }
    [Fact] public async Task Handle_Returns_TechnicalAnalyses() { var list = new List<TechnicalAnalysisEntity> { new() { Id = 1, Name = "SMA", CreatedBy = 1 } }; _dbMock.Setup(x => x.TechnicalAnalyses).Returns(DbSetMockHelper.CreateMockDbSet(list.AsQueryable()).Object); var result = await _handler.Handle(new GetTechnicalAnalysis.Request(), CancellationToken.None); Assert.True(result.IsSuccess); Assert.NotEmpty(result.Value); }
    [Fact] public async Task Handle_Returns_Empty_When_No_Data() { _dbMock.Setup(x => x.TechnicalAnalyses).Returns(DbSetMockHelper.CreateMockDbSet(new List<TechnicalAnalysisEntity>().AsQueryable()).Object); var result = await _handler.Handle(new GetTechnicalAnalysis.Request(), CancellationToken.None); Assert.True(result.IsSuccess); Assert.Empty(result.Value); }
}

public class GetTechnicalAnalysisDetailValidatorTests
{
    private static readonly GetTechnicalAnalysisDetail.Validator _validator = new();
    [Fact] public void Should_Have_Error_When_Id_Is_Zero() { var r = _validator.TestValidate(new GetTechnicalAnalysisDetail.Request(0)); r.ShouldHaveValidationErrorFor(x => x.Id); }
    [Fact] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new GetTechnicalAnalysisDetail.Request(1)); r.ShouldNotHaveAnyValidationErrors(); }
}

public class GetTechnicalAnalysisDetailHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private GetTechnicalAnalysisDetail.Handler _handler = null!;
    public GetTechnicalAnalysisDetailHandlerTests() { _dbMock = new Mock<ITradeDbContext>(); _handler = new GetTechnicalAnalysisDetail.Handler(_dbMock.Object); }
    [Fact] public async Task Handle_Returns_Failure_When_Not_Found() { _dbMock.Setup(x => x.TechnicalAnalyses).Returns(DbSetMockHelper.CreateMockDbSet(new List<TechnicalAnalysisEntity>().AsQueryable()).Object); var result = await _handler.Handle(new GetTechnicalAnalysisDetail.Request(99), CancellationToken.None); Assert.True(result.IsFailure); }
    [Fact] public async Task Handle_Returns_Success_When_Found() { var ta = new TechnicalAnalysisEntity { Id = 1, Name = "SMA", CreatedBy = 1, ShortName = "SMA", Description = "desc" }; _dbMock.Setup(x => x.TechnicalAnalyses).Returns(DbSetMockHelper.CreateMockDbSet(new List<TechnicalAnalysisEntity> { ta }.AsQueryable()).Object); var result = await _handler.Handle(new GetTechnicalAnalysisDetail.Request(1), CancellationToken.None); Assert.True(result.IsSuccess); Assert.Equal("SMA", result.Value.Name); }
}

public class UpdateTechnicalAnalysisValidatorTests
{
    private static readonly UpdateTechnicalAnalysis.Validator _validator = new();
    [Fact] public void Should_Have_Error_When_Name_Is_Empty() { var r = _validator.TestValidate(new UpdateTechnicalAnalysis.Request(1, "", "SMA", "desc", 1)); r.ShouldHaveValidationErrorFor(x => x.Name); }
    [Fact] public void Should_Have_Error_When_Id_Is_Zero() { var r = _validator.TestValidate(new UpdateTechnicalAnalysis.Request(0, "SMA", "RSI", "desc", 1)); r.ShouldHaveValidationErrorFor(x => x.Id); }
    [Fact] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new UpdateTechnicalAnalysis.Request(1, "RSI", "RSI", "Relative Strength Index", 1)); r.ShouldNotHaveAnyValidationErrors(); }
}

public class UpdateTechnicalAnalysisHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private UpdateTechnicalAnalysis.Handler _handler = null!;
    public UpdateTechnicalAnalysisHandlerTests() { _dbMock = new Mock<ITradeDbContext>(); _handler = new UpdateTechnicalAnalysis.Handler(_dbMock.Object); }
    [Fact] public async Task Handle_Returns_Failure_When_Not_Found() { _dbMock.Setup(x => x.TechnicalAnalyses).Returns(DbSetMockHelper.CreateMockDbSet(new List<TechnicalAnalysisEntity>().AsQueryable()).Object); var result = await _handler.Handle(new UpdateTechnicalAnalysis.Request(99, "RSI", "RSI", "desc", 1), CancellationToken.None); Assert.True(result.IsFailure); }
    [Fact] public async Task Handle_Returns_Success_When_Updated() { var entity = new TechnicalAnalysisEntity { Id = 1, CreatedBy = 1 }; var dbSetMock = DbSetMockHelper.CreateMockDbSet(new List<TechnicalAnalysisEntity> { entity }.AsQueryable()); _dbMock.Setup(x => x.TechnicalAnalyses).Returns(dbSetMock.Object); _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1); var result = await _handler.Handle(new UpdateTechnicalAnalysis.Request(1, "Updated", "U", "desc", 1), CancellationToken.None); Assert.True(result.IsSuccess); }
}

public class DeleteTechnicalAnalysisValidatorTests
{
    private static readonly DeleteTechnicalAnalysis.Validator _validator = new();
    [Fact] public void Should_Have_Error_When_Id_Is_Zero() { var r = _validator.TestValidate(new DeleteTechnicalAnalysis.Request(0, 1)); r.ShouldHaveValidationErrorFor(x => x.Id); }
    [Fact] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new DeleteTechnicalAnalysis.Request(1, 1)); r.ShouldNotHaveAnyValidationErrors(); }
}

public class DeleteTechnicalAnalysisHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private DeleteTechnicalAnalysis.Handler _handler = null!;
    public DeleteTechnicalAnalysisHandlerTests() { _dbMock = new Mock<ITradeDbContext>(); _handler = new DeleteTechnicalAnalysis.Handler(_dbMock.Object); }
    [Fact] public async Task Handle_Returns_Failure_When_Not_Found() { _dbMock.Setup(x => x.TechnicalAnalyses).Returns(DbSetMockHelper.CreateMockDbSet(new List<TechnicalAnalysisEntity>().AsQueryable()).Object); var result = await _handler.Handle(new DeleteTechnicalAnalysis.Request(99, 1), CancellationToken.None); Assert.True(result.IsFailure); }
    [Fact] public async Task Handle_Returns_Success_When_Deleted() { var ta = new TechnicalAnalysisEntity { Id = 1, Name = "SMA", CreatedBy = 1 }; _dbMock.Setup(x => x.TechnicalAnalyses).Returns(DbSetMockHelper.CreateMockDbSet(new List<TechnicalAnalysisEntity> { ta }.AsQueryable()).Object); _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1); var result = await _handler.Handle(new DeleteTechnicalAnalysis.Request(1, 1), CancellationToken.None); Assert.True(result.IsSuccess); _dbMock.Verify(x => x.TechnicalAnalyses.Remove(ta), Times.Once); }
}
