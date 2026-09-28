using System.Text.Json;
using System.Text.Json.Serialization;
using LlmUtilityApi.Dependencies;

namespace LlmUtilityApi.Endpoints;

/// <summary>Non-gating dependency report: this service's outward auth seams, served from the
/// poller's cache. Deliberately not part of /readyz.</summary>
public static class DepzEndpoints
{
    // Scoped here: devops-api needs enum names, and the app has no global converter.
    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static void MapDepz(this IEndpointRouteBuilder app) =>
        app.MapGet("/depz", (DependencyReportCache cache) => TypedResults.Json(cache.Current(), WireJson))
            .AllowAnonymous()
            .AddEndpointFilter<ProbeKeyFilter>()
            .ExcludeFromDescription()
            .DisableHttpMetrics()
            .WithName("GetDependencies");
}
