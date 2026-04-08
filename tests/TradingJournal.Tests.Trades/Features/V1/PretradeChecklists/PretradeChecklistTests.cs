using FluentAssertions;
using FluentValidation.TestHelper;
using Moq;
using TradingJournal.Modules.Trades.Common.Enum;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Features.V1.PretradeChecklists;
using TradingJournal.Modules.Trades.Infrastructure;

namespace TradingJournal.Tests.Trades.Features.V1.PretradeChecklists;

[TestFixture]
public class CreatePretradeChecklistValidatorTests
{
    private static readonly CreatePretradeChecklist.Validator _validator = new();
    [Test] public void Should_Have_Error_When_Name_Is_Empty() { var r = _validator.TestValidate(new CreatePretradeChecklist.Request("", PretradeCheckListType.PreTrade)); r.ShouldHaveValidationErrorFor(x => x.Name); }
    [Test] public void Should_Have_Error_When_Type_Is_Invalid() { var r = _validator.TestValidate(new CreatePretradeChecklist.Request("Test", (PretradeCheckListType)99)); r.ShouldHaveValidationErrorFor(x => x.Type); }
    [Test] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new CreatePretradeChecklist.Request("Test", PretradeCheckListType.PreTrade)); r.ShouldNotHaveAnyErrors(); }
}

[TestFixture]
public class CreatePretradeChecklistHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private CreatePretradeChecklist.Handler _handler = null!;
    [SetUp] public void SetUp() { _dbMock = new Mock<ITradeDbContext>(); _handler = new CreatePretradeChecklist.Handler(_dbMock.Object); }
    [Test] public async Task Handle_Returns_Failure_When_UserId_Is_Zero() { var result = await _handler.Handle(new CreatePretradeChecklist.Request("Test", PretradeCheckListType.PreTrade, 0), CancellationToken.None); result.IsFailure.Should().BeTrue(); }
    [Test] public async Task Handle_Returns_Success_When_Valid() { _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1); var result = await _handler.Handle(new CreatePretradeChecklist.Request("Test", PretradeCheckListType.PreTrade, 1), CancellationToken.None); result.IsSuccess.Should().BeTrue(); }
}

[TestFixture]
public class GetPretradeChecklistsHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private GetPretradeChecklists.Handler _handler = null!;
    [SetUp] public void SetUp() { _dbMock = new Mock<ITradeDbContext>(); _handler = new GetPretradeChecklists.Handler(_dbMock.Object); }
    [Test] public async Task Handle_Returns_Checklists_For_UserId() { var list = new List<PretradeChecklist> { new() { Id = 1, Name = "Test", CheckListType = PretradeCheckListType.PreTrade, CreatedBy = 1 } }; _dbMock.Setup(x => x.PretradeChecklists).Returns(list.AsQueryable()); var result = await _handler.Handle(new GetPretradeChecklists.Request(1), CancellationToken.None); result.IsSuccess.Should().BeTrue(); result.Value.Should().NotBeEmpty(); }
    [Test] public async Task Handle_Returns_Empty_When_No_Data() { _dbMock.Setup(x => x.PretradeChecklists).Returns(new List<PretradeChecklist>().AsQueryable()); var result = await _handler.Handle(new GetPretradeChecklists.Request(1), CancellationToken.None); result.IsSuccess.Should().BeTrue(); result.Value.Should().BeEmpty(); }
}

[TestFixture]
public class UpdatePretradeChecklistValidatorTests
{
    private static readonly UpdatePretradeChecklist.Validator _validator = new();
    [Test] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new UpdatePretradeChecklist.Request { Id = 1, Name = "Test", Type = PretradeCheckListType.PreTrade, UserId = 1 }); r.ShouldNotHaveAnyErrors(); }
}

[TestFixture]
public class UpdatePretradeChecklistHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private UpdatePretradeChecklist.Handler _handler = null!;
    [SetUp] public void SetUp() { _dbMock = new Mock<ITradeDbContext>(); _handler = new UpdatePretradeChecklist.Handler(_dbMock.Object); }
    [Test] public async Task Handle_Returns_Failure_When_Not_Found() { _dbMock.Setup(x => x.PretradeChecklists.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((PretradeChecklist?)null); var result = await _handler.Handle(new UpdatePretradeChecklist.Request { Id = 99, Name = "Test", Type = PretradeCheckListType.PreTrade, UserId = 1 }, CancellationToken.None); result.IsFailure.Should().BeTrue(); }
    [Test] public async Task Handle_Returns_Success_When_Updated() { var checklist = new PretradeChecklist { Id = 1, Name = "Old", CreatedBy = 1 }; _dbMock.Setup(x => x.PretradeChecklists.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(checklist); _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1); var result = await _handler.Handle(new UpdatePretradeChecklist.Request { Id = 1, Name = "New", Type = PretradeCheckListType.PreTrade, UserId = 1 }, CancellationToken.None); result.IsSuccess.Should().BeTrue(); }
}

[TestFixture]
public class DeletePretradeChecklistValidatorTests
{
    private static readonly DeletePretradeChecklist.Validator _validator = new();
    [Test] public void Should_Have_Error_When_Id_Is_Zero() { var r = _validator.TestValidate(new DeletePretradeChecklist.Request(0, 1)); r.ShouldHaveValidationErrorFor(x => x.Id); }
    [Test] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new DeletePretradeChecklist.Request(1, 1)); r.ShouldNotHaveAnyErrors(); }
}

[TestFixture]
public class DeletePretradeChecklistHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private DeletePretradeChecklist.Handler _handler = null!;
    [SetUp] public void SetUp() { _dbMock = new Mock<ITradeDbContext>(); _handler = new DeletePretradeChecklist.Handler(_dbMock.Object); }
    [Test] public async Task Handle_Returns_Failure_When_Not_Found() { _dbMock.Setup(x => x.PretradeChecklists.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((PretradeChecklist?)null); var result = await _handler.Handle(new DeletePretradeChecklist.Request(99, 1), CancellationToken.None); result.IsFailure.Should().BeTrue(); }
    [Test] public async Task Handle_Returns_Success_When_Deleted() { var checklist = new PretradeChecklist { Id = 1, Name = "Test", CreatedBy = 1 }; _dbMock.Setup(x => x.PretradeChecklists.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(checklist); _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1); var result = await _handler.Handle(new DeletePretradeChecklist.Request(1, 1), CancellationToken.None); result.IsSuccess.Should().BeTrue(); _dbMock.Verify(x => x.PretradeChecklists.Remove(checklist), Times.Once); }
}
