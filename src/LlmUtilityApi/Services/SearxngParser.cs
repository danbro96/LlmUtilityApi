using System.Text.Json;
using System.Text.Json.Serialization;

namespace LlmUtilityApi.Services;

/// <summary>Pure mapping of a SearXNG JSON response to results — factored out so it is unit-testable with no I/O.</summary>
internal static class SearxngParser
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<SearchResult> Parse(string json, int max)
    {
        var payload = JsonSerializer.Deserialize<Payload>(json, Options);
        if (payload?.Results is not { } raw) return [];

        var results = new List<SearchResult>(Math.Min(max, raw.Count));
        foreach (var r in raw)
        {
            if (string.IsNullOrWhiteSpace(r.Url) || string.IsNullOrWhiteSpace(r.Title)) continue;
            results.Add(new SearchResult(r.Title.Trim(), r.Url, (r.Content ?? string.Empty).Trim()));
            if (results.Count >= max) break;
        }

        return results;
    }

    private sealed class Payload
    {
        [JsonPropertyName("results")] public List<Entry>? Results { get; set; }
    }

    private sealed class Entry
    {
        [JsonPropertyName("title")] public string? Title { get; set; }

        [JsonPropertyName("url")] public string? Url { get; set; }

        [JsonPropertyName("content")] public string? Content { get; set; }
    }
}
