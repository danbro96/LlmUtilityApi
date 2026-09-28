using LlmUtilityApi.Services;

namespace LlmUtilityApi.Dependencies;

/// <summary>Roster derived from the same options the real clients bind — edges cannot drift.</summary>
public static class DependencyTargets
{
    public static IReadOnlyList<DependencyTarget> From(SearchOptions search) =>
    [
        new DependencyTarget
        {
            Name = "searxng",
            BaseUrl = search.BaseUrl ?? string.Empty,
            ProbePath = "healthz",
        },
    ];
}
