namespace PittMoney.PhysicalAssets;

/// <summary>
/// Outcome of validating an uploaded possession photo before it's stored.
/// </summary>
public record ImageUploadValidationResult(bool IsValid, string? Error)
{
    public static ImageUploadValidationResult Success() => new(true, null);

    public static ImageUploadValidationResult Failure(string error) => new(false, error);
}
