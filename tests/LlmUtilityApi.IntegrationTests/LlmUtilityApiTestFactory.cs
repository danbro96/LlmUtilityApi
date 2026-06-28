using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LlmUtilityApi.IntegrationTests;

/// <summary>Hosts the real app in-process for boot/auth smoke tests (no external dependencies).</summary>
public sealed class LlmUtilityApiTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development");
}
