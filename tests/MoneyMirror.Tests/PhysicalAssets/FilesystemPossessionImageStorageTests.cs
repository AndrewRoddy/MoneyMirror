using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MoneyMirror.PhysicalAssets;

namespace MoneyMirror.Tests.PhysicalAssets;

/// <summary>Integration tests: reads/writes real files on disk under a temp directory.</summary>
public class FilesystemPossessionImageStorageTests : IDisposable
{
    private class FakeHostEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "MoneyMirror.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Test";
    }

    private readonly string _tempRoot;
    private readonly FilesystemPossessionImageStorage _storage;

    public FilesystemPossessionImageStorageTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "MoneyMirror-tests-" + Guid.NewGuid().ToString("N"));

        var environment = new FakeHostEnvironment { ContentRootPath = _tempRoot };
        var options = Options.Create(new ImageUploadOptions { StorageDirectory = "images" });
        _storage = new FilesystemPossessionImageStorage(options, environment);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_ThenOpenReadAsync_RoundTripsTheSameBytes()
    {
        byte[] original = [1, 2, 3, 4, 5];
        using var input = new MemoryStream(original);

        var reference = await _storage.SaveAsync(input, "photo.jpg");

        await using var readBack = await _storage.OpenReadAsync(reference);
        Assert.NotNull(readBack);

        using var output = new MemoryStream();
        await readBack!.CopyToAsync(output);

        Assert.Equal(original, output.ToArray());
    }

    [Fact]
    public async Task SaveAsync_ReturnsReferenceEndingInOriginalExtension()
    {
        using var input = new MemoryStream([1]);

        var reference = await _storage.SaveAsync(input, "photo.PNG");

        Assert.EndsWith(".png", reference);
    }

    [Fact]
    public async Task OpenReadAsync_UnknownReference_ReturnsNull()
    {
        var result = await _storage.OpenReadAsync("does-not-exist.jpg");

        Assert.Null(result);
    }

    [Fact]
    public async Task OpenReadAsync_PathTraversalAttempt_DoesNotEscapeStorageDirectory()
    {
        var result = await _storage.OpenReadAsync("../../etc/passwd");

        Assert.Null(result);
    }
}

