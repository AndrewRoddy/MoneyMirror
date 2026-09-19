namespace MoneyMirror.HumanCapital;

/// <summary>
/// Outcome of validating an uploaded resume before it's handed to
/// <see cref="IResumeTextExtractionService"/>.
/// </summary>
public record ResumeUploadValidationResult(bool IsValid, string? Error)
{
    public static ResumeUploadValidationResult Success() => new(true, null);

    public static ResumeUploadValidationResult Failure(string error) => new(false, error);
}

