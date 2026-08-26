namespace LlmUtilityApi.Mcp;

public sealed record DiceResult(IReadOnlyList<int> Rolls, int Total);
