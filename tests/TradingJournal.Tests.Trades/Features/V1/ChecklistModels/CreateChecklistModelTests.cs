using TradingJournal.Tests.Trades.Helpers;
using FluentValidation.TestHelper;
using Moq;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Features.V1.ChecklistModels;
using TradingJournal.Modules.Trades.Infrastructure;

namespace TradingJournal.Tests.Trades.Features.V1.ChecklistModels;

public class CreateChecklistModelValidatorTests
{
    private static readonly CreateChecklistModel.Validator _validator = new();
    [Fact]
    public void Should_Have_Error_When_Name_Is_Null()
    {
        var request = new CreateChecklistModel.Request(null!, "desc");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }
    [Fact]
    public void Should_Have_Error_When_Name_Is_Empty()
    {
        var request = new CreateChecklistModel.Request("", "desc");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }
    [Fact]
    public void Should_Not_Have_Error_When_Valid()
    {
        var request = new CreateChecklistModel.Request("Pre-Market Checklist", "description");
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }
}

public class CreateChecklistModelHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private CreateChecklistModel.Handler _handler = null!;
    public CreateChecklistModelHandlerTests()
    {
        _dbMock = new Mock<ITradeDbContext>();
        _handler = new CreateChecklistModel.Handler(_dbMock.Object);
    }
    [Fact]
    public async Task Handle_Returns_Failure_When_UserId_Is_Zero()
    {
        var request = new CreateChecklistModel.Request("Test", "desc", 0);
        var result = await _handler.Handle(request, CancellationToken.None);
        Assert.True(result.IsFailure);
    }
    [Fact]
    public async Task Handle_Returns_Success_When_Valid()
    {
        _dbMock.Setup(x => x.ChecklistModels).Returns(DbSetMockHelper.CreateMockDbSet(new List<ChecklistModel>().AsQueryable()).Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var request = new CreateChecklistModel.Request("Test", "desc", 1);
        var result = await _handler.Handle(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        _dbMock.Verify(x => x.ChecklistModels.AddAsync(It.IsAny<ChecklistModel>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
