using TradingJournal.Tests.Trades.Helpers;
using FluentValidation.TestHelper;
using Moq;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Features.V1.ChecklistModels;
using TradingJournal.Shared.Dtos;
using TradingJournal.Modules.Trades.Infrastructure;

namespace TradingJournal.Tests.Trades.Features.V1.ChecklistModels;

[TestFixture]
public class UpdateChecklistModelValidatorTests
{
    private static readonly UpdateChecklistModel.Validator _validator = new();
    [Test] public void Should_Have_Error_When_Id_Is_Zero() { var r = _validator.TestValidate(new UpdateChecklistModel.Request(0, "x", null, 1)); r.ShouldHaveValidationErrorFor(x => x.Id); }
    [Test] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new UpdateChecklistModel.Request(1, "x", null, 1)); r.ShouldNotHaveAnyValidationErrors(); }
}

[TestFixture]
public class UpdateChecklistModelHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private UpdateChecklistModel.Handler _handler = null!;
    [SetUp] public void SetUp() { _dbMock = new Mock<ITradeDbContext>(); _handler = new UpdateChecklistModel.Handler(_dbMock.Object); }
    [Test] public async Task Handle_Returns_Failure_When_Not_Found() { _dbMock.Setup(x => x.ChecklistModels).Returns(DbSetMockHelper.CreateMockDbSet(new List<ChecklistModel>().AsQueryable()).Object); var result = await _handler.Handle(new UpdateChecklistModel.Request(99, "x", null, 1), CancellationToken.None); Assert.That(result.IsFailure, Is.True); }
    [Test] public async Task Handle_Returns_Success_When_Found() { var model = new ChecklistModel { Id = 1, Name = "Old", CreatedBy = 1 }; _dbMock.Setup(x => x.ChecklistModels).Returns(DbSetMockHelper.CreateMockDbSet(new List<ChecklistModel> { model }.AsQueryable()).Object); _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1); var result = await _handler.Handle(new UpdateChecklistModel.Request(1, "New", null, 1), CancellationToken.None); Assert.That(result.IsSuccess, Is.True); }
}
