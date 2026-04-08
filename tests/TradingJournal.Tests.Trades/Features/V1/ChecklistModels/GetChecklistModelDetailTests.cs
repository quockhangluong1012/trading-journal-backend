using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Linq.Expressions;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Features.V1.ChecklistModels;
using TradingJournal.Modules.Trades.Infrastructure;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

namespace TradingJournal.Tests.Trades.Features.V1.ChecklistModels;

[TestFixture]
public class GetChecklistModelDetailValidatorTests
{
    private static readonly GetChecklistModelDetail.Validator _validator = new();
    [Test]
    public void Should_Have_Error_When_Id_Is_Zero()
    {
        var result = _validator.TestValidate(new GetChecklistModelDetail.Request(0, 1));
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }
    [Test]
    public void Should_Not_Have_Error_When_Valid()
    {
        var result = _validator.TestValidate(new GetChecklistModelDetail.Request(1, 1));
        result.ShouldNotHaveAnyValidationErrors();
    }
}

[TestFixture]
public class GetChecklistModelDetailHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private GetChecklistModelDetail.Handler _handler = null!;
    [SetUp]
    public void SetUp()
    {
        _dbMock = new Mock<ITradeDbContext>();
        _handler = new GetChecklistModelDetail.Handler(_dbMock.Object);
    }
    [Test]
    public async Task Handle_Returns_Failure_When_Not_Found()
    {
        var checklistModel = new ChecklistModel { Id = 1, Name = "Test", Criteria = new List<PretradeChecklist>() };
        var checkListSet = new Mock<DbSet<ChecklistModel>>();
        checkListSet.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<ChecklistModel, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(checklistModel);
        checkListSet.Setup(x => x.Include(It.IsAny<string>())).Returns(checkListSet.Object);
        _dbMock.Setup(x => x.ChecklistModels).Returns(checkListSet.Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(new GetChecklistModelDetail.Request(1, 1), CancellationToken.None);
        result.IsFailure.Should().BeTrue();
    }
    [Test]
    public async Task Handle_Returns_Success_When_Found()
    {
        var checklistModel = new ChecklistModel { Id = 1, Name = "Test", Criteria = new List<PretradeChecklist>() };
        var checkListSet = new Mock<DbSet<ChecklistModel>>();
        checkListSet.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<ChecklistModel, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(checklistModel);
        checkListSet.Setup(x => x.Include(It.IsAny<string>())).Returns(checkListSet.Object);
        _dbMock.Setup(x => x.ChecklistModels).Returns(checkListSet.Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(new GetChecklistModelDetail.Request(1, 1), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Test");
    }
}
