using Microsoft.Extensions.Options;
using MoneyMirror.PhysicalAssets;

namespace MoneyMirror.Tests.PhysicalAssets;

public class ImageUploadValidatorTests
{
    private readonly ImageUploadValidator _validator = new(
        Options.Create(new ImageUploadOptions { MaxFileSizeBytes = 1024 }));

    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("photo.jpeg")]
    [InlineData("photo.png")]
    [InlineData("photo.webp")]
    [InlineData("photo.gif")]
    [InlineData("Photo.JPG")]
    public void Validate_SupportedImageTypeWithinSizeLimit_Succeeds(string fileName)
    {
        var result = _validator.Validate(fileName, fileSizeBytes: 512);

        Assert.True(result.IsValid);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData("resume.pdf")]
    [InlineData("photo.bmp")]
    [InlineData("photo")]
    public void Validate_UnsupportedExtension_FailsWithClearMessage(string fileName)
    {
        var result = _validator.Validate(fileName, fileSizeBytes: 512);

        Assert.False(result.IsValid);
        Assert.Contains("Unsupported file type", result.Error);
    }

    [Fact]
    public void Validate_FileTooLarge_FailsWithClearMessage()
    {
        var result = _validator.Validate("photo.jpg", fileSizeBytes: 2048);

        Assert.False(result.IsValid);
        Assert.Contains("too large", result.Error);
    }

    [Fact]
    public void Validate_EmptyFile_FailsWithClearMessage()
    {
        var result = _validator.Validate("photo.jpg", fileSizeBytes: 0);

        Assert.False(result.IsValid);
        Assert.Contains("empty", result.Error);
    }
}

