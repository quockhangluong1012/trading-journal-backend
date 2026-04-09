using TradingJournal.Tests.Trades.Helpers;
using FluentValidation.TestHelper;
using Moq;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Features.V1.ChecklistModels;
using TradingJournal.Modules.Trades.Infrastructure;

namespace TradingJournal.Tests.Trades.Features.V1.ChecklistModels;

[TestFixture]
public class AddCriteriaToModelValidatorTests
{
    private static readonly AddCriteriaToModel.Validator _validator = new();
    [Test]
    public void Should_Have_Error_When_ChecklistModelId_Is_Zero()
    {
        var request = new AddCriteriaToModel.Request(0, "test", Modules.Trades.Common.Enum.PretradeChecklistType.MarketStructure);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ModelId);
    }
    [Test]
    public void Should_Not_Have_Error_When_Valid()
    {
        var request = new AddCriteriaToModel.Request(1, "test", Modules.Trades.Common.Enum.PretradeChecklistType.MarketStructure);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}

[TestFixture]
public class AddCriteriaToModelHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private AddCriteriaToModel.Handler _handler = null!;
    [SetUp]
    public void SetUp()
    {
        _dbMock = new Mock<ITradeDbContext>();
        _handler = new AddCriteriaToModel.Handler(_dbMock.Object);
    }
    [Test]
    public async Task Handle_Returns_Failure_When_Model_Not_Found()
    {
        _dbMock.Setup(x => x.ChecklistModels).Returns(DbSetMockHelper.CreateMockDbSet(new List<ChecklistModel>().AsQueryable()).Object);
        var result = await _handler.Handle(new AddCriteriaToModel.Request(99, "test", Modules.Trades.Common.Enum.PretradeChecklistType.MarketStructure), CancellationToken.None);
        Assert.That(result.IsFailure, Is.True);
    }
    [Test]
    public async Task Handle_Returns_Success_When_Model_Exists()
    {
        var model = new ChecklistModel { Id = 1, Name = "Test", CreatedBy = 1 };
        _dbMock.Setup(x => x.ChecklistModels).Returns(DbSetMockHelper.CreateMockDbSet(new List<ChecklistModel> { model }.AsQueryable()).Object);
        _dbMock.Setup(x => x.PretradeChecklists).Returns(DbSetMockHelper.CreateMockDbSet(new List<PretradeChecklist>().AsQueryable()).Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var result = await _handler.Handle(new AddCriteriaToModel.Request(1, "test", Modules.Trades.Common.Enum.PretradeChecklistType.MarketStructure), CancellationToken.None);
        Assert.That(result.IsSuccess, Is.True);
    }
}
