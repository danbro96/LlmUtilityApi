using System.Text.Json.Nodes;

namespace LlmUtilityApi.Mcp;

public sealed record JwtParts(JsonNode? Header, JsonNode? Payload);
