//using FluentAssertions;
//using FluentValidation.TestHelper;
//using Moq;
//using TradingJournal.Modules.Trades.Domain;
//using TradingJournal.Modules.Trades.Features.V1.ChecklistModels;
//using TradingJournal.Modules.Trades.Infrastructure;

//namespace TradingJournal.Tests.Trades.Features.V1.ChecklistModels;

//[TestFixture]
//public class AddCriteriaToModelValidatorTests
//{
//    private static readonly AddCriteriaToModel.Validator _validator = new();
//    [Test]
//    public void Should_Have_Error_When_ChecklistModelId_Is_Zero()
//    {
//        var request = new AddCriteriaToModel.Request { ChecklistModelId = 0, Name = "test", Order = 1, UserId = 1 };
//        var result = _validator.TestValidate(request);
//        result.ShouldHaveValidationErrorFor(x => x.ChecklistModelId);
//    }
//    [Test]
//    public void Should_Not_Have_Error_When_Valid()
//    {
//        var request = new AddCriteriaToModel.Request { ChecklistModelId = 1, Name = "test", Order = 1, UserId = 1 };
//        var result = _validator.TestValidate(request);
//        result.ShouldNotHaveAnyErrors();
//    }
//}

//[TestFixture]
//public class AddCriteriaToModelHandlerTests
//{
//    private Mock<ITradeDbContext> _dbMock = null!;
//    private AddCriteriaToModel.Handler _handler = null!;
//    [SetUp]
//    public void SetUp()
//    {
//        _dbMock = new Mock<ITradeDbContext>();
//        _handler = new AddCriteriaToModel.Handler(_dbMock.Object);
//    }
//    [Test]
//    public async Task Handle_Returns_Failure_When_Model_Not_Found()
//    {
//        _dbMock.Setup(x => x.ChecklistModels.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
//            .ReturnsAsync((ChecklistModel?)null);
//        var result = await _handler.Handle(new AddCriteriaToModel.Request { ChecklistModelId = 99, Name = "test", Order = 1, UserId = 1 }, CancellationToken.None);
//        result.IsFailure.Should().BeTrue();
//    }
//    [Test]
//    public async Task Handle_Returns_Success_When_Model_Exists()
//    {
//        _dbMock.Setup(x => x.ChecklistModels.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
//            .ReturnsAsync(new ChecklistModel { Id = 1, Name = "Test", CreatedBy = 1 });
//        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
//        var result = await _handler.Handle(new AddCriteriaToModel.Request { ChecklistModelId = 1, Name = "test", Order = 1, UserId = 1 }, CancellationToken.None);
//        result.IsSuccess.Should().BeTrue();
//    }
//}
