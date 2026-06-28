namespace LlmUtilityApi.Services;

/// <summary>
/// Splits text into overlapping character windows for embedding/RAG ingestion. Each chunk is at
/// most <c>maxChars</c>; consecutive chunks share <c>overlap</c> characters. Within the last
/// ~15% of a window the cut is nudged to the nearest whitespace so words aren't split mid-token.
/// </summary>
public static class TextChunker
{
    public static IReadOnlyList<string> Chunk(string text, int maxChars, int overlap)
    {
        if (maxChars <= 0) throw new ArgumentOutOfRangeException(nameof(maxChars), "maxChars must be > 0");
        if (overlap < 0 || overlap >= maxChars)
            throw new ArgumentOutOfRangeException(nameof(overlap), "overlap must be >= 0 and < maxChars");

        text = text.Trim();
        if (text.Length == 0) return [];
        if (text.Length <= maxChars) return [text];

        var chunks = new List<string>();
        var start = 0;
        while (start < text.Length)
        {
            var end = Math.Min(start + maxChars, text.Length);
            if (end < text.Length)
            {
                var window = Math.Max(1, maxChars * 15 / 100);
                var lastBreak = text.LastIndexOf(' ', end - 1, Math.Min(window, end - start));
                if (lastBreak > start) end = lastBreak;
            }

            chunks.Add(text[start..end].Trim());
            if (end >= text.Length) break;
            start = Math.Max(end - overlap, start + 1);
        }

        return chunks;
    }
}
