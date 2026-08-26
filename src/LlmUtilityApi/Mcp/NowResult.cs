namespace LlmUtilityApi.Mcp;

public sealed record NowResult(string Utc, long Unix, string? Timezone, string? Local);
