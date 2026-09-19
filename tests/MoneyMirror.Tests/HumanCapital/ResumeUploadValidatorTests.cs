using Microsoft.Extensions.Options;
using MoneyMirror.HumanCapital;

namespace MoneyMirror.Tests.HumanCapital;

public class ResumeUploadValidatorTests
{
    private readonly ResumeUploadValidator _validator = new(
        Options.Create(new ResumeUploadOptions { MaxFileSizeBytes = 1024 }));

    [Theory]
    [InlineData("resume.pdf")]
    [InlineData("resume.docx")]
    [InlineData("Resume.PDF")]
    public void Validate_SupportedTypeWithinSizeLimit_Succeeds(string fileName)
    {
        var result = _validator.Validate(fileName, fileSizeBytes: 512);

        Assert.True(result.IsValid);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData("resume.txt")]
    [InlineData("resume.doc")]
    [InlineData("resume")]
    public void Validate_UnsupportedExtension_FailsWithClearMessage(string fileName)
    {
        var result = _validator.Validate(fileName, fileSizeBytes: 512);

        Assert.False(result.IsValid);
        Assert.Contains("Unsupported file type", result.Error);
    }

    [Fact]
    public void Validate_FileTooLarge_FailsWithClearMessage()
    {
        var result = _validator.Validate("resume.pdf", fileSizeBytes: 2048);

        Assert.False(result.IsValid);
        Assert.Contains("too large", result.Error);
    }

    [Fact]
    public void Validate_EmptyFile_FailsWithClearMessage()
    {
        var result = _validator.Validate("resume.pdf", fileSizeBytes: 0);

        Assert.False(result.IsValid);
        Assert.Contains("empty", result.Error);
    }
}

