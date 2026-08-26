using Microsoft.Extensions.Options;

namespace LlmUtilityApi.Services;

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
