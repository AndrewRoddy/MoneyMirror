using MoneyMirror.HumanCapital;

namespace MoneyMirror.Tests.HumanCapital;

/// <summary>
/// Integration tests: parses real PDF/DOCX fixtures on disk end to end
/// (no mocking of the parsing libraries).
/// </summary>
public class ResumeTextExtractionServiceTests
{
    private readonly IResumeTextExtractionService _service = new ResumeTextExtractionService();

    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);

    [Fact]
    public async Task ExtractTextAsync_Pdf_ReturnsDocumentContent()
    {
        await using var stream = File.OpenRead(FixturePath("sample_resume.pdf"));

        var text = await _service.ExtractTextAsync(stream, "sample_resume.pdf");

        Assert.Contains("Jane Doe", text);
        Assert.Contains("Software Engineer", text);
        Assert.Contains("University of Pittsburgh", text);
    }

    [Fact]
    public async Task ExtractTextAsync_Docx_ReturnsDocumentContent()
    {
        await using var stream = File.OpenRead(FixturePath("sample_resume.docx"));

        var text = await _service.ExtractTextAsync(stream, "sample_resume.docx");

        Assert.Contains("Jane Doe", text);
        Assert.Contains("Software Engineer", text);
        Assert.Contains("University of Pittsburgh", text);
    }

    [Fact]
    public async Task ExtractTextAsync_UnsupportedExtension_ThrowsResumeTextExtractionException()
    {
        await using var stream = File.OpenRead(FixturePath("sample_resume.txt"));

        var ex = await Assert.ThrowsAsync<ResumeTextExtractionException>(
            () => _service.ExtractTextAsync(stream, "sample_resume.txt"));

        Assert.Contains(".txt", ex.Message);
    }

    // #187: Real cause of "Failed to parse the PDF document" - a password-protected
    // PDF makes PdfPig throw PdfDocumentEncryptedException, which the generic
    // catch previously mislabeled as a corrupt/unsupported file.
    [Fact]
    public async Task ExtractTextAsync_PasswordProtectedPdf_ThrowsWithActionableMessage()
    {
        await using var stream = File.OpenRead(FixturePath("password_protected_resume.pdf"));

        var ex = await Assert.ThrowsAsync<ResumeTextExtractionException>(
            () => _service.ExtractTextAsync(stream, "password_protected_resume.pdf"));

        Assert.Contains("password", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // #187: the real cause. IBrowserFile.OpenReadStream() on Blazor Server hands
    // back a BrowserFileStream: forward-only, and a synchronous Read throws
    // outright. PdfPig and Open XML both read synchronously and seek freely - to
    // a PDF's cross-reference table, to a DOCX's ZIP central directory - so the
    // service has to buffer the upload before parsing it. BlazorServerStream
    // below reproduces that shape; a plain seekable FileStream cannot.
    [Fact]
    public async Task ExtractTextAsync_PdfFromNonSeekableStream_ReturnsDocumentContent()
    {
        await using var stream = new BlazorServerStream(await File.ReadAllBytesAsync(FixturePath("sample_resume.pdf")));

        var text = await _service.ExtractTextAsync(stream, "sample_resume.pdf");

        Assert.Contains("Jane Doe", text);
    }

    [Fact]
    public async Task ExtractTextAsync_DocxFromNonSeekableStream_ReturnsDocumentContent()
    {
        await using var stream = new BlazorServerStream(await File.ReadAllBytesAsync(FixturePath("sample_resume.docx")));

        var text = await _service.ExtractTextAsync(stream, "sample_resume.docx");

        Assert.Contains("Jane Doe", text);
    }

    /// <summary>
    /// Mirrors Microsoft.AspNetCore.Components.Forms.BrowserFileStream, which is
    /// what an upload actually arrives on under Blazor Server. Read-only,
    /// non-seekable, and - the part that matters - synchronous reads throw.
    /// </summary>
    private sealed class BlazorServerStream(byte[] content) : Stream
    {
        private readonly MemoryStream _inner = new(content);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException("Synchronous reads are not supported.");

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

