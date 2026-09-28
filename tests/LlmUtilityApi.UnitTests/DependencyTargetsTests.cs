using LlmUtilityApi.Dependencies;
using LlmUtilityApi.Services;
using Xunit;

namespace LlmUtilityApi.UnitTests;

public class DependencyTargetsTests
{
    [Fact]
    public void Searxng_edge_uses_search_base_url_and_healthz()
    {
        var target = Assert.Single(DependencyTargets.From(new SearchOptions { BaseUrl = "http://searxng:8080" }));

        Assert.Equal("searxng", target.Name);
        Assert.Equal("http://searxng:8080", target.BaseUrl);
        Assert.Equal("healthz", target.ProbePath);
    }

    [Fact]
    public void Unset_search_base_url_yields_blank_edge()
    {
        var target = Assert.Single(DependencyTargets.From(new SearchOptions()));

        Assert.Equal(string.Empty, target.BaseUrl);
    }
}
