using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Moq;
using TradingJournal.Modules.Trades.Features.V1.Screenshot;

namespace TradingJournal.Tests.Trades.Features.V1.Screenshot;

[TestFixture]
public class UploadScreenShootValidatorTests
{
    private static readonly UploadScreenShoot.Validator _validator = new();
    [Test]
    public void Should_Have_Error_When_File_Is_Null()
    {
        var request = new UploadScreenShoot.Request(null!);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.File);
    }
    [Test]
    public void Should_Not_Have_Error_When_File_Is_Provided()
    {
        var mockFile = new Mock<IFormFile>();
        var request = new UploadScreenShoot.Request(mockFile.Object);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.File);
    }
}

[TestFixture]
public class UploadScreenShootHandlerTests
{
    private Mock<IWebHostEnvironment> _envMock = null!;
    private Mock<IHttpContextAccessor> _httpContextAccessorMock = null!;
    private UploadScreenShoot.Handler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _envMock = new Mock<IWebHostEnvironment>();
        _envMock.Setup(x => x.ContentRootPath).Returns("/tmp");
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _handler = new UploadScreenShoot.Handler(_envMock.Object, _httpContextAccessorMock.Object);
    }

    [Test]
    public async Task Handle_Returns_Failure_When_File_Is_Null()
    {
        var request = new UploadScreenShoot.Request(null!);
        var result = await _handler.Handle(request, CancellationToken.None);
        result.IsFailure.Should().BeTrue();
    }
}
