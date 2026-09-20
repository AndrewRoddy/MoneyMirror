using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Exceptions;

namespace MoneyMirror.HumanCapital;

/// <summary>
/// <see cref="IResumeTextExtractionService"/> implementation supporting PDF
/// (via PdfPig) and DOCX (via the Open XML SDK).
/// </summary>
public class ResumeTextExtractionService : IResumeTextExtractionService
{
    public Task<string> ExtractTextAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        cancellationToken.ThrowIfCancellationRequested();

        var text = extension switch
        {
            ".pdf" => ExtractFromPdf(fileStream),
            ".docx" => ExtractFromDocx(fileStream),
            _ => throw new ResumeTextExtractionException(
                $"Unsupported file type '{extension}'. Only .pdf and .docx are supported."),
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ResumeTextExtractionException(
                $"No text could be extracted from '{fileName}'. The document may be empty, image-only, or corrupt.");
        }

        return Task.FromResult(text);
    }

    private static string ExtractFromPdf(Stream fileStream)
    {
        try
        {
            using var document = PdfDocument.Open(fileStream);
            return string.Join("\n\n", document.GetPages().Select(page => page.Text));
        }
        catch (ResumeTextExtractionException)
        {
            throw;
        }
        catch (PdfDocumentEncryptedException ex)
        {
            throw new ResumeTextExtractionException(
                "This PDF is password-protected. Please remove the password and upload it again.", ex);
        }
        catch (Exception ex)
        {
            throw new ResumeTextExtractionException("Failed to parse the PDF document.", ex);
        }
    }

    private static string ExtractFromDocx(Stream fileStream)
    {
        try
        {
            using var wordDocument = WordprocessingDocument.Open(fileStream, isEditable: false);
            var body = wordDocument.MainDocumentPart?.Document?.Body;
            if (body is null)
            {
                return string.Empty;
            }

            return string.Join(
                "\n",
                body.Descendants<Paragraph>().Select(paragraph => paragraph.InnerText));
        }
        catch (ResumeTextExtractionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ResumeTextExtractionException("Failed to parse the DOCX document.", ex);
        }
    }
}

