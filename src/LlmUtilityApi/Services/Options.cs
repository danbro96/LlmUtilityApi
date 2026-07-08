namespace LlmUtilityApi.Services;

public sealed class FetchOptions
{
    /// <summary>Hard cap on bytes read from a fetched response (defends against huge bodies).</summary>
    public long MaxResponseBytes { get; set; } = 5 * 1024 * 1024;

    public int TimeoutSeconds { get; set; } = 20;

    public int MaxRedirects { get; set; } = 5;

    /// <summary>When false (default), connections to private/loopback/link-local IPs are refused (SSRF guard).</summary>
    public bool AllowPrivateNetworks { get; set; }
}

public sealed class DocOptions
{
    /// <summary>Hard cap on the decoded document size accepted by the extraction tools.</summary>
    public long MaxBytes { get; set; } = 20 * 1024 * 1024;
}

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
