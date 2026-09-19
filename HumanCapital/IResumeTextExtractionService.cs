namespace MoneyMirror.HumanCapital;

/// <summary>
/// Extracts plain text from an uploaded resume document (PDF or DOCX) so it
/// can be handed to the profile-extraction step. No parsing of the extracted
/// text into a professional profile happens here - that's a later stage.
/// </summary>
public interface IResumeTextExtractionService
{
    /// <summary>
    /// Reads <paramref name="fileStream"/> and returns its plain text content.
    /// The document type is inferred from <paramref name="fileName"/>'s extension.
    /// </summary>
    /// <exception cref="ResumeTextExtractionException">
    /// The file type isn't supported, or the document couldn't be parsed.
    /// </exception>
    Task<string> ExtractTextAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken = default);
}
