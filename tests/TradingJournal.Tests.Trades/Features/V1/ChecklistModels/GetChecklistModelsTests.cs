using TradingJournal.Tests.Trades.Helpers;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Linq.Expressions;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Features.V1.ChecklistModels;
using TradingJournal.Modules.Trades.Infrastructure;

namespace TradingJournal.Tests.Trades.Features.V1.ChecklistModels;

[TestFixture]
public class GetChecklistModelsValidatorTests
{
    private static readonly GetChecklistModels.Validator _validator = new();
    [Test]
    public void Should_Have_Error_When_Page_Is_Zero()
    {
        var request = new GetChecklistModels.Request { PageIndex = 0, PageSize = 10 };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PageIndex);
    }
    [Test]
    public void Should_Have_Error_When_PageSize_Is_Zero()
    {
        var request = new GetChecklistModels.Request { PageIndex = 1, PageSize = 0 };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }
    [Test]
    public void Should_Not_Have_Error_When_Valid()
    {
        var request = new GetChecklistModels.Request { PageIndex = 1, PageSize = 10 };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.PageIndex);
        result.ShouldNotHaveValidationErrorFor(x => x.PageSize);
    }
}

[TestFixture]
public class GetChecklistModelsHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private GetChecklistModels.Handler _handler = null!;
    [SetUp]
    public void SetUp()
    {
        _dbMock = new Mock<ITradeDbContext>();
        _handler = new GetChecklistModels.Handler(_dbMock.Object);
    }
    [Test]
    public async Task Handle_Returns_Paginated_Results()
    {
        var models = new List<ChecklistModel> { new ChecklistModel { Id = 1, Name = "Test1", CreatedBy = 1 }, new ChecklistModel { Id = 2, Name = "Test2", CreatedBy = 1 } };
        _dbMock.Setup(x => x.ChecklistModels).Returns(DbSetMockHelper.CreateMockDbSet(models.AsQueryable()).Object);

        var request = new GetChecklistModels.Request { PageIndex = 1, PageSize = 10, UserId = 1 };
        var result = await _handler.Handle(request, CancellationToken.None);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Count, Is.EqualTo(2));
    }
    [Test]
    public async Task Handle_Filters_By_UserId()
    {
        var models = new List<ChecklistModel> { new ChecklistModel { Id = 1, Name = "Test1", CreatedBy = 1 }, new ChecklistModel { Id = 2, Name = "Test2", CreatedBy = 2 } };
        _dbMock.Setup(x => x.ChecklistModels).Returns(DbSetMockHelper.CreateMockDbSet(models.AsQueryable()).Object);

        var request = new GetChecklistModels.Request { PageIndex = 1, PageSize = 10, UserId = 1 };
        var result = await _handler.Handle(request, CancellationToken.None);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Count, Is.EqualTo(2));
    }
}
