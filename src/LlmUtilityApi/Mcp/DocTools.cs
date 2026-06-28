using System.ComponentModel;
using LlmUtilityApi.Services;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace LlmUtilityApi.Mcp;

[McpServerToolType]
public sealed class DocTools
{
    private readonly DocOptions _opts;

    public DocTools(IOptions<DocOptions> opts) => _opts = opts.Value;

    [McpServerTool(Name = "extract_text")]
    [Description("Extract plain text from a base64-encoded document. Detects PDF, Word (.docx), HTML, or plain text " +
        "(by the optional filename/media-type hint, else by content).")]
    public ExtractResult ExtractText(
        [Description("The document bytes, base64-encoded.")] string contentBase64,
        [Description("A filename or media type to hint the format, e.g. 'report.pdf' or 'application/pdf' (optional).")] string? hint = null)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(contentBase64);
        }
        catch (FormatException)
        {
            throw new McpException("contentBase64 is not valid base64");
        }

        if (bytes.LongLength > _opts.MaxBytes)
            throw new McpException($"document exceeds the {_opts.MaxBytes}-byte limit");
        if (bytes.Length == 0)
            throw new McpException("document is empty");

        try
        {
            var (kind, text) = DocExtractor.Extract(bytes, hint);
            return new ExtractResult(kind.ToString().ToLowerInvariant(), text, text.Length);
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"could not extract text: {ex.Message}");
        }
    }

    [McpServerTool(Name = "chunk")]
    [Description("Split text into overlapping character chunks for embedding/RAG. Cuts are nudged to whitespace.")]
    public ChunkResult Chunk(
        [Description("The text to chunk.")] string text,
        [Description("Maximum characters per chunk (default 2000).")] int maxChars = 2000,
        [Description("Characters of overlap between consecutive chunks (default 200).")] int overlap = 200)
    {
        try
        {
            var chunks = TextChunker.Chunk(text, maxChars, overlap);
            return new ChunkResult(chunks.Count, chunks);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new McpException(ex.Message);
        }
    }
}

public sealed record ExtractResult(string Kind, string Text, int Length);

public sealed record ChunkResult(int Count, IReadOnlyList<string> Chunks);
