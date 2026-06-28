using System.Text;
using AngleSharp.Html.Parser;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace LlmUtilityApi.Services;

public enum DocKind
{
    Pdf,
    Docx,
    Html,
    Text,
}

/// <summary>Extracts plain text from a document's bytes (PDF, Word .docx, HTML, or plain text).</summary>
public static class DocExtractor
{
    public static DocKind Detect(byte[] bytes, string? hint)
    {
        var h = hint?.ToLowerInvariant() ?? string.Empty;
        if (h.Contains("pdf")) return DocKind.Pdf;
        if (h.Contains("word") || h.Contains("docx") || h.Contains("officedocument.wordprocessing")) return DocKind.Docx;
        if (h.Contains("html") || h.Contains("htm")) return DocKind.Html;
        if (h.Contains("text") || h.Contains(".txt") || h.Contains(".md")) return DocKind.Text;

        if (bytes.Length >= 4)
        {
            if (bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46) return DocKind.Pdf; // %PDF
            if (bytes[0] == 0x50 && bytes[1] == 0x4B && bytes[2] == 0x03 && bytes[3] == 0x04) return DocKind.Docx; // PK.. (OOXML zip)
        }

        var head = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 256)).TrimStart();
        if (head.StartsWith('<')) return DocKind.Html;
        return DocKind.Text;
    }

    public static (DocKind Kind, string Text) Extract(byte[] bytes, string? hint)
    {
        var kind = Detect(bytes, hint);
        var text = kind switch
        {
            DocKind.Pdf => ExtractPdf(bytes),
            DocKind.Docx => ExtractDocx(bytes),
            DocKind.Html => ExtractHtml(bytes),
            _ => Encoding.UTF8.GetString(bytes),
        };
        return (kind, text.Trim());
    }

    private static string ExtractPdf(byte[] bytes)
    {
        using var doc = PdfDocument.Open(bytes);
        var sb = new StringBuilder();
        foreach (var page in doc.GetPages())
        {
            sb.AppendLine(page.Text);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string ExtractDocx(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes, writable: false);
        using var doc = WordprocessingDocument.Open(ms, isEditable: false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return string.Empty;
        return string.Join('\n', body.Descendants<Paragraph>().Select(p => p.InnerText));
    }

    private static string ExtractHtml(byte[] bytes)
    {
        var html = Encoding.UTF8.GetString(bytes);
        var document = new HtmlParser().ParseDocument(html);
        return document.Body?.TextContent ?? document.DocumentElement.TextContent;
    }
}
