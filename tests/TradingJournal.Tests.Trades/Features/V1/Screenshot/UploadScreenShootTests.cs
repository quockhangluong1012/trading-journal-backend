using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Http;
using Moq;
using TradingJournal.Modules.Trades.Features.V1.Screenshot;
using TradingJournal.Modules.Trades.Services;
using TradingJournal.Shared.Abstractions;

namespace TradingJournal.Tests.Trades.Features.V1.Screenshot;

public class UploadScreenShootValidatorTests
{
    private static readonly UploadScreenShoot.Validator _validator = new();

    [Fact]
    public void Should_Have_Error_When_File_Is_Null()
    {
        var request = new UploadScreenShoot.Request(null!);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.File);
    }

    [Fact]
    public void Should_Not_Have_Error_When_File_Is_Provided()
    {
        var mockFile = CreateFormFileMock(length: 1024, contentType: "image/png");
        var request = new UploadScreenShoot.Request(mockFile.Object);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.File);
    }

    [Fact]
    public void Should_Have_Error_When_File_Is_Empty()
    {
        var request = new UploadScreenShoot.Request(CreateFormFileMock(length: 0, contentType: "image/png").Object);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.File);
    }

    [Fact]
    public void Should_Have_Error_When_File_Is_Too_Large()
    {
        var request = new UploadScreenShoot.Request(CreateFormFileMock(length: 11 * 1024 * 1024, contentType: "image/png").Object);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.File);
    }

    [Fact]
    public void Should_Have_Error_When_Content_Type_Is_Invalid()
    {
        var request = new UploadScreenShoot.Request(CreateFormFileMock(length: 1024, contentType: "application/pdf").Object);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.File);
    }

    private static Mock<IFormFile> CreateFormFileMock(long length, string contentType)
    {
        Mock<IFormFile> mockFile = new();
        mockFile.SetupGet(x => x.Length).Returns(length);
        mockFile.SetupGet(x => x.ContentType).Returns(contentType);
        return mockFile;
    }
}

public class UploadScreenShootHandlerTests
{
    private Mock<IScreenshotService> _screenshotServiceMock = null!;
    private UploadScreenShoot.Handler _handler = null!;

    public UploadScreenShootHandlerTests()
    {
        _screenshotServiceMock = new Mock<IScreenshotService>();
        _handler = new UploadScreenShoot.Handler(_screenshotServiceMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_File_Is_Null()
    {
        var request = new UploadScreenShoot.Request(null!);
        var result = await _handler.Handle(request, CancellationToken.None);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Returns_Success_When_Screenshot_Service_Saves_File()
    {
        byte[] imageBytes = [0x89, 0x50, 0x4E, 0x47];
        Mock<IFormFile> formFile = CreateFormFileMock(imageBytes, "image/png");
        _screenshotServiceMock
            .Setup(service => service.SaveScreenshotAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/screenshots/test.png");

        Result<string> result = await _handler.Handle(new UploadScreenShoot.Request(formFile.Object), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("/screenshots/test.png", result.Value);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Screenshot_Service_Rejects_File()
    {
        byte[] imageBytes = [0x00, 0x01, 0x02, 0x03];
        Mock<IFormFile> formFile = CreateFormFileMock(imageBytes, "image/png");
        _screenshotServiceMock
            .Setup(service => service.SaveScreenshotAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Uploaded file is invalid."));

        Result<string> result = await _handler.Handle(new UploadScreenShoot.Request(formFile.Object), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    private static Mock<IFormFile> CreateFormFileMock(byte[] bytes, string contentType)
    {
        MemoryStream stream = new(bytes);
        Mock<IFormFile> mockFile = new();
        mockFile.SetupGet(x => x.Length).Returns(bytes.Length);
        mockFile.SetupGet(x => x.ContentType).Returns(contentType);
        mockFile.Setup(x => x.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns<Stream, CancellationToken>(async (target, cancellationToken) =>
            {
                stream.Position = 0;
                await stream.CopyToAsync(target, cancellationToken);
            });
        return mockFile;
    }
}
