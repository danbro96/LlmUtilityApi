namespace LlmUtilityApi.Services;

public sealed record WebSearchResult(string Query, int Count, IReadOnlyList<SearchResult> Results);
