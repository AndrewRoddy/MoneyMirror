namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// Stores possession photos and returns an opaque reference later steps
/// (detection, valuation, inventory) can use to retrieve them.
/// </summary>
public interface IPossessionImageStorage
{
    /// <summary>
    /// Saves <paramref name="content"/> and returns a reference to it.
    /// </summary>
    /// <exception cref="PossessionImageStorageException">The file couldn't be saved.</exception>
    Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a previously saved image for reading, or <c>null</c> if
    /// <paramref name="reference"/> doesn't exist.
    /// </summary>
    Task<Stream?> OpenReadAsync(string reference, CancellationToken cancellationToken = default);
}

