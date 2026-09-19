namespace PittMoney.Ai;

/// <summary>
/// Thin seam over the multimodal vision model provider: an image (plus an
/// instruction prompt) in, a text detection response out. Feature code (e.g.
/// physical-asset detection) parses that response into its own structured
/// types; this seam does no feature-specific interpretation.
/// </summary>
public interface IVisionService
{
    /// <summary>
    /// Sends <paramref name="imageBytes"/> (of type <paramref name="mediaType"/>, e.g.
    /// "image/jpeg") to the vision model along with <paramref name="prompt"/> and
    /// returns its text response.
    /// </summary>
    /// <exception cref="VisionServiceException">
    /// The provider call failed, returned an error, or returned an unusable response.
    /// </exception>
    Task<string> DetectAsync(
        byte[] imageBytes,
        string mediaType,
        string prompt,
        CancellationToken cancellationToken = default);
}
