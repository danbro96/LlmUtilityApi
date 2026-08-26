namespace LlmUtilityApi.Mcp;

public sealed record ChunkResult(int Count, IReadOnlyList<string> Chunks);
