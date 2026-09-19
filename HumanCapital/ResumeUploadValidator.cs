using Microsoft.Extensions.Options;

namespace PittMoney.HumanCapital;

/// <summary>
/// <see cref="IResumeUploadValidator"/> implementation: extension allow-list
/// plus a configurable max size.
/// </summary>
public class ResumeUploadValidator : IResumeUploadValidator
{
    private static readonly string[] AllowedExtensions = [".pdf", ".docx"];

    private readonly ResumeUploadOptions _options;

    public ResumeUploadValidator(IOptions<ResumeUploadOptions> options)
    {
        _options = options.Value;
    }

    public ResumeUploadValidationResult Validate(string fileName, long fileSizeBytes)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
        {
            return ResumeUploadValidationResult.Failure(
                $"Unsupported file type '{extension}'. Only PDF (.pdf) and Word (.docx) resumes are accepted.");
        }

        if (fileSizeBytes <= 0)
        {
            return ResumeUploadValidationResult.Failure("The uploaded file is empty.");
        }

        if (fileSizeBytes > _options.MaxFileSizeBytes)
        {
            var maxMb = _options.MaxFileSizeBytes / (1024.0 * 1024.0);
            return ResumeUploadValidationResult.Failure(
                $"File is too large ({fileSizeBytes / (1024.0 * 1024.0):F1} MB). Maximum allowed size is {maxMb:F0} MB.");
        }

        return ResumeUploadValidationResult.Success();
    }
}
