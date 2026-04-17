using FluentValidation.TestHelper;
using Moq;
using TradingJournal.Modules.Trades.Common.Enum;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Features.V1.PretradeChecklists;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Tests.Trades.Helpers;

namespace TradingJournal.Tests.Trades.Features.V1.PretradeChecklists;

public class CreatePretradeChecklistValidatorTests
{
    private static readonly CreatePretradeChecklist.Validator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Name_Is_Empty()
    {
        var result = _validator.TestValidate(new CreatePretradeChecklist.Request("", PretradeChecklistType.TradingSetup, 1));
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Have_Error_When_Type_Is_Invalid()
    {
        var result = _validator.TestValidate(new CreatePretradeChecklist.Request("Test", (PretradeChecklistType)99, 1));
        result.ShouldHaveValidationErrorFor(x => x.Type);
    }

    [Fact]
    public void Should_Have_Error_When_Model_Id_Is_Zero()
    {
        var result = _validator.TestValidate(new CreatePretradeChecklist.Request("Test", PretradeChecklistType.TradingSetup, 0));
        result.ShouldHaveValidationErrorFor(x => x.ChecklistModelId);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Valid()
    {
        var result = _validator.TestValidate(new CreatePretradeChecklist.Request("Test", PretradeChecklistType.TradingSetup, 1));
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class CreatePretradeChecklistHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private CreatePretradeChecklist.Handler _handler = null!;

    public CreatePretradeChecklistHandlerTests()
    {
        _dbMock = new Mock<ITradeDbContext>();
        _handler = new CreatePretradeChecklist.Handler(_dbMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_UserId_Is_Zero()
    {
        var result = await _handler.Handle(new CreatePretradeChecklist.Request("Test", PretradeChecklistType.TradingSetup, 1, 0), CancellationToken.None);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Model_Belongs_To_Another_User()
    {
        var models = new List<ChecklistModel>
        {
            new() { Id = 1, Name = "Plan", CreatedBy = 2 },
        };

        _dbMock.Setup(x => x.ChecklistModels).Returns(DbSetMockHelper.CreateMockDbSet(models.AsQueryable()).Object);

        var result = await _handler.Handle(new CreatePretradeChecklist.Request("Test", PretradeChecklistType.TradingSetup, 1, 1), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Returns_Success_When_Valid()
    {
        var models = new List<ChecklistModel>
        {
            new() { Id = 1, Name = "Plan", CreatedBy = 1 },
        };

        _dbMock.Setup(x => x.ChecklistModels).Returns(DbSetMockHelper.CreateMockDbSet(models.AsQueryable()).Object);
        _dbMock.Setup(x => x.PretradeChecklists).Returns(DbSetMockHelper.CreateMockDbSet(new List<PretradeChecklist>().AsQueryable()).Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(new CreatePretradeChecklist.Request("Test", PretradeChecklistType.TradingSetup, 1, 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }
}

public class GetPretradeChecklistsHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private GetPretradeChecklists.Handler _handler = null!;

    public GetPretradeChecklistsHandlerTests()
    {
        _dbMock = new Mock<ITradeDbContext>();
        _handler = new GetPretradeChecklists.Handler(_dbMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Checklists_For_UserId()
    {
        var userModel = new ChecklistModel { Id = 1, Name = "Plan A", CreatedBy = 1 };
        var otherModel = new ChecklistModel { Id = 2, Name = "Plan B", CreatedBy = 2 };
        var list = new List<PretradeChecklist>
        {
            new() { Id = 1, Name = "Test", CheckListType = PretradeChecklistType.TradingSetup, ChecklistModelId = 1, ChecklistModel = userModel },
            new() { Id = 2, Name = "Other", CheckListType = PretradeChecklistType.Psychology, ChecklistModelId = 2, ChecklistModel = otherModel },
        };

        _dbMock.Setup(x => x.PretradeChecklists).Returns(DbSetMockHelper.CreateMockDbSet(list.AsQueryable()).Object);

        var result = await _handler.Handle(new GetPretradeChecklists.Request(1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(1, result.Value.Single().ChecklistModelId);
    }

    [Fact]
    public async Task Handle_Returns_Empty_When_No_Data()
    {
        _dbMock.Setup(x => x.PretradeChecklists).Returns(DbSetMockHelper.CreateMockDbSet(new List<PretradeChecklist>().AsQueryable()).Object);

        var result = await _handler.Handle(new GetPretradeChecklists.Request(1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }
}

public class UpdatePretradeChecklistValidatorTests
{
    private static readonly UpdatePretradeChecklist.Validator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_Valid()
    {
        var result = _validator.TestValidate(new UpdatePretradeChecklist.Request(1, "Test", PretradeChecklistType.TradingSetup, 1, 1));
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class UpdatePretradeChecklistHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private UpdatePretradeChecklist.Handler _handler = null!;

    public UpdatePretradeChecklistHandlerTests()
    {
        _dbMock = new Mock<ITradeDbContext>();
        _handler = new UpdatePretradeChecklist.Handler(_dbMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Not_Found()
    {
        _dbMock.Setup(x => x.PretradeChecklists).Returns(DbSetMockHelper.CreateMockDbSet(new List<PretradeChecklist>().AsQueryable()).Object);
        _dbMock.Setup(x => x.ChecklistModels).Returns(DbSetMockHelper.CreateMockDbSet(new List<ChecklistModel>().AsQueryable()).Object);

        var result = await _handler.Handle(new UpdatePretradeChecklist.Request(99, "New", PretradeChecklistType.TradingSetup, 1, 1), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Returns_Success_When_Updated()
    {
        var model = new ChecklistModel { Id = 1, Name = "Plan", CreatedBy = 1 };
        var checklist = new PretradeChecklist { Id = 1, ChecklistModelId = 1, ChecklistModel = model };
        var dbSetMock = DbSetMockHelper.CreateMockDbSet(new List<PretradeChecklist> { checklist }.AsQueryable());

        _dbMock.Setup(x => x.PretradeChecklists).Returns(dbSetMock.Object);
        _dbMock.Setup(x => x.ChecklistModels).Returns(DbSetMockHelper.CreateMockDbSet(new List<ChecklistModel> { model }.AsQueryable()).Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(new UpdatePretradeChecklist.Request(1, "Test", PretradeChecklistType.TradingSetup, 1, 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Test", checklist.Name);
    }
}

public class DeletePretradeChecklistValidatorTests
{
    private static readonly DeletePretradeChecklist.Validator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Id_Is_Zero()
    {
        var result = _validator.TestValidate(new DeletePretradeChecklist.Request(0, 1));
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Valid()
    {
        var result = _validator.TestValidate(new DeletePretradeChecklist.Request(1, 1));
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class DeletePretradeChecklistHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private DeletePretradeChecklist.Handler _handler = null!;

    public DeletePretradeChecklistHandlerTests()
    {
        _dbMock = new Mock<ITradeDbContext>();
        _handler = new DeletePretradeChecklist.Handler(_dbMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Not_Found()
    {
        _dbMock.Setup(x => x.PretradeChecklists).Returns(DbSetMockHelper.CreateMockDbSet(new List<PretradeChecklist>().AsQueryable()).Object);
        _dbMock.Setup(x => x.TradeHistoryChecklist).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistoryChecklist>().AsQueryable()).Object);

        var result = await _handler.Handle(new DeletePretradeChecklist.Request(99, 1), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Checklist_Is_Used_In_Trade_History()
    {
        var model = new ChecklistModel { Id = 1, Name = "Plan", CreatedBy = 1 };
        var checklist = new PretradeChecklist { Id = 1, Name = "Test", ChecklistModelId = 1, ChecklistModel = model };

        _dbMock.Setup(x => x.PretradeChecklists).Returns(DbSetMockHelper.CreateMockDbSet(new List<PretradeChecklist> { checklist }.AsQueryable()).Object);
        _dbMock.Setup(x => x.TradeHistoryChecklist).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistoryChecklist>
        {
            new() { Id = 1, PretradeChecklistId = 1, TradeHistoryId = 1 }
        }.AsQueryable()).Object);

        var result = await _handler.Handle(new DeletePretradeChecklist.Request(1, 1), CancellationToken.None);

        Assert.True(result.IsFailure);
        _dbMock.Verify(x => x.PretradeChecklists.Remove(It.IsAny<PretradeChecklist>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Returns_Success_When_Deleted()
    {
        var model = new ChecklistModel { Id = 1, Name = "Plan", CreatedBy = 1 };
        var checklist = new PretradeChecklist { Id = 1, Name = "Test", ChecklistModelId = 1, ChecklistModel = model };

        _dbMock.Setup(x => x.PretradeChecklists).Returns(DbSetMockHelper.CreateMockDbSet(new List<PretradeChecklist> { checklist }.AsQueryable()).Object);
        _dbMock.Setup(x => x.TradeHistoryChecklist).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistoryChecklist>().AsQueryable()).Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(new DeletePretradeChecklist.Request(1, 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _dbMock.Verify(x => x.PretradeChecklists.Remove(checklist), Times.Once);
    }
}
