using LlmUtilityApi.Auth;
using LlmUtilityApi.Endpoints;
using LlmUtilityApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ApiKeyAuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.Configure<FetchOptions>(builder.Configuration.GetSection("Fetch"));
builder.Services.Configure<DocOptions>(builder.Configuration.GetSection("Doc"));
builder.Services.Configure<SearchOptions>(builder.Configuration.GetSection("Search"));

// The web-fetch tool: an HttpClient whose connections are pinned to public IPs by an SSRF guard
// (see SafeFetchService), with auto-redirect re-validated per hop.
builder.Services.AddSingleton<SafeFetchService>();

// The web-search tool: queries a trusted, admin-configured SearXNG endpoint (no SSRF guard — the
// endpoint may be a LAN instance). Empty Search:BaseUrl => the tool errors when called.
builder.Services.AddSingleton<WebSearchService>();

// MCP agent surface. The [McpServerToolType] tool groups in this assembly are mounted at /mcp over
// Streamable HTTP, secured by the same X-API-Key scheme (see MapMcp below), and kept
// LAN/WireGuard-only — never published through the Cloudflare Tunnel.
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// Liveness (/livez) + readiness (/readyz) probes. Stateless service — both are process-up only.
builder.Services.AddAppHealthChecks();

builder.Services
    .AddAuthentication(ApiKeyAuthOptions.SchemeName)
    .AddScheme<ApiKeyAuthOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthOptions.SchemeName, opts =>
    {
        var section = builder.Configuration.GetSection("Auth");
        section.Bind(opts);
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var permitsPerMinute = builder.Configuration.GetValue("RateLimit:RequestsPerMinute", 120);
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var key = ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anon";
        return RateLimitPartition.GetTokenBucketLimiter(key, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = permitsPerMinute,
            TokensPerPeriod = permitsPerMinute,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    });
});

var allowedOrigins = builder.Configuration.GetSection("Auth:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
        p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));
}

// Doc-extraction tools accept a base64-encoded file in the MCP request body; allow headroom for
// the base64 inflation (~4/3) over the configured raw cap.
var docMaxBytes = builder.Configuration.GetValue("Doc:MaxBytes", 20 * 1024 * 1024);
builder.WebHost.ConfigureKestrel(o =>
    o.Limits.MaxRequestBodySize = (long) (docMaxBytes * 4 / 3) + (256 * 1024));

builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, context, _) =>
    {
        document.Info = new()
        {
            Title = "LlmUtilityApi",
            Version = "v1",
            Description =
                "Self-hosted utility tools for LLM agents, exposed over MCP at /mcp (LAN-only). " +
                "Pure deterministic tools (math, time, json, text, crypto, random) plus web-search " +
                "(SearXNG), web-fetch, and document extraction. Authenticate with your key in the " +
                "X-API-Key header.",
        };
        document.Components ??= new();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["ApiKey"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = ApiKeyAuthOptions.HeaderName,
            Description = "API key. Send in the X-API-Key header.",
        };
        return Task.CompletedTask;
    });
});

var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
if (!string.IsNullOrWhiteSpace(otlpEndpoint))
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService(
            serviceName: "llm-utility-api",
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0"))
        .WithTracing(t => t
            .AddSource("LlmUtilityApi.*")
            .AddAspNetCoreInstrumentation(o => o.RecordException = true)
            .AddHttpClientInstrumentation()
            .AddOtlpExporter())
        .WithMetrics(m => m
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter());

    builder.Logging.AddOpenTelemetry(o =>
    {
        o.IncludeFormattedMessage = true;
        o.IncludeScopes = true;
        o.AddOtlpExporter();
    });
}

var app = builder.Build();

if (allowedOrigins.Length > 0) app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Keep the MCP surface LAN/WireGuard-only: 404 any /mcp request that arrived via the
// Cloudflare Tunnel (backstop behind the tunnel ingress not routing /mcp at all).
app.UseMcpLanOnly();

app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
app.MapScalarApiReference("/scalar", o => o
        .WithTitle("LlmUtilityApi")
        .WithTheme(ScalarTheme.BluePlanet))
    .AllowAnonymous();

app.MapAppHealthChecks(app.Environment);

// Agent MCP surface (Streamable HTTP). Mapped AFTER UseAuthentication/UseAuthorization so the
// same X-API-Key scheme validates it; RequireAuthorization rejects anonymous calls with 401.
// Exposure is LAN/WireGuard-only — the Cloudflare Tunnel must not route /mcp (see deploy notes).
app.MapMcp("/mcp").RequireAuthorization();

app.Run();

// Exposed for WebApplicationFactory<Program> in the integration tests.
public partial class Program;
