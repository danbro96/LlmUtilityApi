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
