namespace MoneyMirror.PhysicalAssets;

/// <summary>Maps a stored possession image's file extension to its media type.</summary>
public static class PossessionImageContentType
{
    public static string FromFileName(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "application/octet-stream",
    };
}

