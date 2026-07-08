using System.ComponentModel;
using LlmUtilityApi.Services;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace LlmUtilityApi.Mcp;

[McpServerToolType]
public sealed class SearchTools
{
    private readonly WebSearchService _search;

    public SearchTools(WebSearchService search) => _search = search;

    [McpServerTool(Name = "web_search")]
    [Description("Search the web via the configured SearXNG instance and return ranked results " +
        "(title, url, snippet). Follow up with fetch_url on a chosen result to read the page.")]
    public async Task<WebSearchResult> WebSearch(
        [Description("Search query.")] string query,
        [Description("Max results to return (default 5).")] int count = 5,
        CancellationToken ct = default)
    {
        try
        {
            return await _search.SearchAsync(query, count, ct);
        }
        catch (ArgumentException ex)
        {
            throw new McpException(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            throw new McpException($"search failed: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            throw new McpException("search timed out");
        }
    }
}
