namespace PittMoney.HumanCapital;

/// <summary>
/// Tunables for resume upload validation, bound from the
/// "HumanCapital:ResumeUpload" section.
/// </summary>
public class ResumeUploadOptions
{
    public const string SectionName = "HumanCapital:ResumeUpload";

    /// <summary>Maximum accepted upload size, in bytes. Defaults to 10 MB.</summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
}
