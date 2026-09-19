using Microsoft.Extensions.Options;

namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// <see cref="IImageUploadValidator"/> implementation: extension allow-list
/// plus a configurable max size.
/// </summary>
public class ImageUploadValidator : IImageUploadValidator
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];

    private readonly ImageUploadOptions _options;

    public ImageUploadValidator(IOptions<ImageUploadOptions> options)
    {
        _options = options.Value;
    }

    public ImageUploadValidationResult Validate(string fileName, long fileSizeBytes)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
        {
            return ImageUploadValidationResult.Failure(
                $"Unsupported file type '{extension}'. Only JPEG, PNG, WEBP, and GIF images are accepted.");
        }

        if (fileSizeBytes <= 0)
        {
            return ImageUploadValidationResult.Failure("The uploaded file is empty.");
        }

        if (fileSizeBytes > _options.MaxFileSizeBytes)
        {
            var maxMb = _options.MaxFileSizeBytes / (1024.0 * 1024.0);
            return ImageUploadValidationResult.Failure(
                $"File is too large ({fileSizeBytes / (1024.0 * 1024.0):F1} MB). Maximum allowed size is {maxMb:F0} MB.");
        }

        return ImageUploadValidationResult.Success();
    }
}
