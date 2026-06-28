using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using SmartReader;

namespace LlmUtilityApi.Services;

public sealed class FetchResult
{
    public required string Url { get; init; }

    public string? Title { get; init; }

    public string? Byline { get; init; }

    public string? SiteName { get; init; }

    public required string Content { get; init; }

    public int Length { get; init; }

    public string? ContentType { get; init; }

    public bool Truncated { get; init; }
}

/// <summary>
/// Fetches a URL with an SSRF guard: every TCP connection (including each redirect hop) is pinned to
/// a publicly-routable address by <see cref="IpGuard"/>, so a hostname can't be pointed at LAN/cloud
/// metadata. HTML is reduced to readable text via SmartReader; other content types return raw text.
/// The response body is read under a hard byte cap.
/// </summary>
public sealed class SafeFetchService
{
    private readonly FetchOptions _opts;
    private readonly HttpClient _http;

    public SafeFetchService(IOptions<FetchOptions> opts)
    {
        _opts = opts.Value;
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = Math.Max(1, _opts.MaxRedirects),
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            ConnectCallback = SafeConnectAsync,
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(_opts.TimeoutSeconds) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("LlmUtilityApi-fetch/1.0");
    }

    public async Task<FetchResult> FetchAsync(string url, bool raw, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("url must be an absolute http(s) URL");

        using var resp = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var contentType = resp.Content.Headers.ContentType?.MediaType;

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var (text, truncated) = await ReadCappedAsync(stream, _opts.MaxResponseBytes, ct);

        var isHtml = contentType?.Contains("html", StringComparison.OrdinalIgnoreCase) ?? false;
        if (raw || !isHtml)
        {
            return new FetchResult
            {
                Url = uri.ToString(),
                Content = text,
                Length = text.Length,
                ContentType = contentType,
                Truncated = truncated,
            };
        }

        var article = new Reader(uri.ToString(), text).GetArticle();
        var content = (article.IsReadable ? article.TextContent : text) ?? text;
        content = content.Trim();
        return new FetchResult
        {
            Url = uri.ToString(),
            Title = article.Title,
            Byline = article.Byline,
            SiteName = article.SiteName,
            Content = content,
            Length = content.Length,
            ContentType = contentType,
            Truncated = truncated,
        };
    }

    private async ValueTask<Stream> SafeConnectAsync(SocketsHttpConnectionContext ctx, CancellationToken ct)
    {
        var host = ctx.DnsEndPoint.Host;
        var port = ctx.DnsEndPoint.Port;
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, ct);

        foreach (var ip in addresses)
        {
            if (!_opts.AllowPrivateNetworks && IpGuard.IsBlocked(ip)) continue;
            try
            {
                var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                await socket.ConnectAsync(new IPEndPoint(ip, port), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException)
            {
                // Try the next candidate address.
            }
        }

        throw new HttpRequestException($"refusing to connect to '{host}': no allowed (public) address");
    }

    private static async Task<(string Text, bool Truncated)> ReadCappedAsync(Stream s, long cap, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;
        var truncated = false;
        int read;
        while ((read = await s.ReadAsync(buffer, ct)) > 0)
        {
            if (total + read > cap)
            {
                ms.Write(buffer, 0, (int) (cap - total));
                truncated = true;
                break;
            }

            ms.Write(buffer, 0, read);
            total += read;
        }

        return (Encoding.UTF8.GetString(ms.ToArray()), truncated);
    }
}
