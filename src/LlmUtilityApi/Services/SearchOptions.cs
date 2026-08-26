namespace LlmUtilityApi.Services;

public sealed class SearchOptions
{
    /// <summary>SearXNG base URL (e.g. http://searxng:8080). A trusted, admin-set endpoint — unlike
    /// fetch it is not SSRF-guarded, so it may point at a LAN instance. Empty = the tool errors on use.</summary>
    public string? BaseUrl { get; set; }

    public int MaxResults { get; set; } = 10;

    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>Optional SearXNG language filter (e.g. "en", "sv"); passed through when set.</summary>
    public string? Language { get; set; }
}
