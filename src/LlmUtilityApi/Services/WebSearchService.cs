using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace LlmUtilityApi.Services;

public sealed record SearchResult(string Title, string Url, string Snippet);

public sealed record WebSearchResult(string Query, int Count, IReadOnlyList<SearchResult> Results);

/// <summary>
/// Queries a self-hosted SearXNG instance (JSON API) and maps its results to <see cref="SearchResult"/>.
/// Unlike <see cref="SafeFetchService"/> there is no SSRF guard: the endpoint is a single, admin-configured,
/// trusted URL (<see cref="SearchOptions.BaseUrl"/>) that is expected to be on the LAN.
/// </summary>
public sealed class WebSearchService
{
    private readonly SearchOptions _opts;
    private readonly HttpClient _http;

    public WebSearchService(IOptions<SearchOptions> opts)
    {
        _opts = opts.Value;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(_opts.TimeoutSeconds) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("LlmUtilityApi-search/1.0");
    }

    public async Task<WebSearchResult> SearchAsync(string query, int count, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("query must not be empty");
        if (string.IsNullOrWhiteSpace(_opts.BaseUrl))
            throw new ArgumentException("search backend not configured");

        var url = $"{_opts.BaseUrl.TrimEnd('/')}/search?q={Uri.EscapeDataString(query)}&format=json&safesearch=1";
        if (!string.IsNullOrWhiteSpace(_opts.Language))
            url += $"&language={Uri.EscapeDataString(_opts.Language)}";

        var json = await _http.GetStringAsync(url, ct);
        var take = Math.Clamp(count, 1, _opts.MaxResults);
        var results = SearxngParser.Parse(json, take);
        return new WebSearchResult(query, results.Count, results);
    }
}

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
