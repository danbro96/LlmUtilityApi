namespace LlmUtilityApi.Dependencies;

/// <summary>One outward edge. The real clients call their dependencies anonymously, so the probe does too.</summary>
public sealed class DependencyTarget
{
    public required string Name { get; set; }

    public required string BaseUrl { get; set; }

    public required string ProbePath { get; set; }
}
