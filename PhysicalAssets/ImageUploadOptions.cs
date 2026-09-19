namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// Tunables for possession image uploads, bound from the
/// "PhysicalAssets:ImageUpload" section.
/// </summary>
public class ImageUploadOptions
{
    public const string SectionName = "PhysicalAssets:ImageUpload";

    /// <summary>Maximum accepted upload size, in bytes. Defaults to 10 MB.</summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Directory images are stored under, relative to the app's content root.
    /// Local filesystem storage is sufficient for this single-user app.
    /// </summary>
    public string StorageDirectory { get; set; } = "App_Data/possession-images";
}
