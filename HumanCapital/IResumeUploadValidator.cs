namespace MoneyMirror.HumanCapital;

/// <summary>
/// Basic file-type/size validation for an uploaded resume, applied before
/// the file is read for text extraction. Does no parsing itself.
/// </summary>
public interface IResumeUploadValidator
{
    /// <summary>
    /// Checks <paramref name="fileName"/>'s extension against the supported
    /// resume types (.pdf, .docx) and <paramref name="fileSizeBytes"/>
    /// against the configured size limit.
    /// </summary>
    ResumeUploadValidationResult Validate(string fileName, long fileSizeBytes);
}
