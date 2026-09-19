namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// Basic file-type/size validation for an uploaded possession photo,
/// applied before the file is stored. This is not a security workstream -
/// just enough to reject obviously wrong uploads with a clear message.
/// </summary>
public interface IImageUploadValidator
{
    ImageUploadValidationResult Validate(string fileName, long fileSizeBytes);
}

