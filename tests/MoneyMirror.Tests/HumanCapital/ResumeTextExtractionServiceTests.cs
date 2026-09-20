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

    // #187: Blazor Server's IBrowserFile.OpenReadStream() returns a forward-only
    // stream that throws on Seek - the same shape PdfPig/Open XML need to read a
    // PDF's cross-reference table or a DOCX's ZIP central directory. These tests
    // simulate that stream instead of File.OpenRead's (seekable) FileStream.
    [Fact]
    public async Task ExtractTextAsync_PdfFromNonSeekableStream_ReturnsDocumentContent()
    {
        await using var stream = new ForwardOnlyStream(await File.ReadAllBytesAsync(FixturePath("sample_resume.pdf")));

        var text = await _service.ExtractTextAsync(stream, "sample_resume.pdf");

        Assert.Contains("Jane Doe", text);
    }

    [Fact]
    public async Task ExtractTextAsync_DocxFromNonSeekableStream_ReturnsDocumentContent()
    {
        await using var stream = new ForwardOnlyStream(await File.ReadAllBytesAsync(FixturePath("sample_resume.docx")));

        var text = await _service.ExtractTextAsync(stream, "sample_resume.docx");

        Assert.Contains("Jane Doe", text);
    }

    private sealed class ForwardOnlyStream(byte[] content) : Stream
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

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
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

