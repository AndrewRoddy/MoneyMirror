using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace PittMoney.PhysicalAssets;

/// <summary>
/// <see cref="IPossessionImageStorage"/> implementation storing images on
/// the local filesystem under the app's content root - sufficient for this
/// single-user app (no blob storage service needed).
/// </summary>
public class FilesystemPossessionImageStorage : IPossessionImageStorage
{
    private readonly string _storageDirectory;

    public FilesystemPossessionImageStorage(IOptions<ImageUploadOptions> options, IHostEnvironment environment)
    {
        _storageDirectory = Path.Combine(environment.ContentRootPath, options.Value.StorageDirectory);
    }

    public async Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        var reference = $"{Guid.NewGuid():N}{Path.GetExtension(fileName).ToLowerInvariant()}";
        var path = Path.Combine(_storageDirectory, reference);

        try
        {
            Directory.CreateDirectory(_storageDirectory);
            await using var fileStream = new FileStream(
                path, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
            await content.CopyToAsync(fileStream, cancellationToken);
        }
        catch (IOException ex)
        {
            throw new PossessionImageStorageException($"Failed to save image '{fileName}'.", ex);
        }

        return reference;
    }

    public Task<Stream?> OpenReadAsync(string reference, CancellationToken cancellationToken = default)
    {
        // Reference always comes from SaveAsync's own Guid-based naming, but this
        // is reachable from an HTTP route parameter - strip any path components
        // defensively so a crafted reference can't escape the storage directory.
        var safeName = Path.GetFileName(reference);
        var path = Path.Combine(_storageDirectory, safeName);

        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        return Task.FromResult<Stream?>(stream);
    }
}
