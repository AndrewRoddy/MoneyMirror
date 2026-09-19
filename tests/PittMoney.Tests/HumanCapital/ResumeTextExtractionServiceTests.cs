using PittMoney.HumanCapital;

namespace PittMoney.Tests.HumanCapital;

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
}
