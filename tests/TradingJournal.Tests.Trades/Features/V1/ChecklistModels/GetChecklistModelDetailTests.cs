using TradingJournal.Tests.Trades.Helpers;
using FluentValidation.TestHelper;
using Moq;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Features.V1.ChecklistModels;
using TradingJournal.Modules.Trades.Infrastructure;

namespace TradingJournal.Tests.Trades.Features.V1.ChecklistModels;

public class GetChecklistModelDetailValidatorTests
{
    private static readonly GetChecklistModelDetail.Validator _validator = new();
    [Fact]
    public void Should_Have_Error_When_Id_Is_Zero()
    {
        var result = _validator.TestValidate(new GetChecklistModelDetail.Request(0, 1));
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }
    [Fact]
    public void Should_Not_Have_Error_When_Valid()
    {
        var result = _validator.TestValidate(new GetChecklistModelDetail.Request(1, 1));
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class GetChecklistModelDetailHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private GetChecklistModelDetail.Handler _handler = null!;
    public GetChecklistModelDetailHandlerTests()
    {
        _dbMock = new Mock<ITradeDbContext>();
        _handler = new GetChecklistModelDetail.Handler(_dbMock.Object);
    }
    [Fact]
    public async Task Handle_Returns_Failure_When_Not_Found()
    {
        _dbMock.Setup(x => x.ChecklistModels).Returns(DbSetMockHelper.CreateMockDbSet(new List<ChecklistModel>().AsQueryable()).Object);
        var result = await _handler.Handle(new GetChecklistModelDetail.Request(1, 1), CancellationToken.None);
        Assert.True(result.IsFailure);
    }
    [Fact]
    public async Task Handle_Returns_Success_When_Found()
    {
        var checklistModel = new ChecklistModel { Id = 1, Name = "Test", Criteria = new List<PretradeChecklist>(), CreatedBy = 1 };
        _dbMock.Setup(x => x.ChecklistModels).Returns(DbSetMockHelper.CreateMockDbSet(new List<ChecklistModel> { checklistModel }.AsQueryable()).Object);
        var result = await _handler.Handle(new GetChecklistModelDetail.Request(1, 1), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("Test", result.Value.Name);
    }
}
