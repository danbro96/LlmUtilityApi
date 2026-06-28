using System.ComponentModel;
using LlmUtilityApi.Services;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace LlmUtilityApi.Mcp;

[McpServerToolType]
public sealed class FetchTools
{
    private readonly SafeFetchService _fetch;

    public FetchTools(SafeFetchService fetch) => _fetch = fetch;

    [McpServerTool(Name = "fetch_url")]
    [Description("Fetch a web page over HTTP(S) and return its readable text. HTML is reduced to the main " +
        "article text (title/byline included); other content types return the raw body. Private, loopback, " +
        "and link-local addresses are refused (SSRF guard), and the body is read under a size cap.")]
    public async Task<FetchResult> FetchUrl(
        [Description("Absolute http(s) URL to fetch.")] string url,
        [Description("Return the raw body instead of extracted article text (default false).")] bool raw = false,
        CancellationToken ct = default)
    {
        try
        {
            return await _fetch.FetchAsync(url, raw, ct);
        }
        catch (ArgumentException ex)
        {
            throw new McpException(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            throw new McpException($"fetch failed: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            throw new McpException("fetch timed out");
        }
    }
}
