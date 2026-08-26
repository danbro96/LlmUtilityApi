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
