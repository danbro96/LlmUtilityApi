namespace LlmUtilityApi.Mcp;

public sealed record ValidateResult(bool Valid, IReadOnlyList<string> Errors);
