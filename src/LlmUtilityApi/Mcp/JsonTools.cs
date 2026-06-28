using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Path;
using Json.Schema;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace LlmUtilityApi.Mcp;

[McpServerToolType]
public static class JsonTools
{
    [McpServerTool(Name = "json_query")]
    [Description("Query a JSON document with a JSONPath expression (e.g. '$.items[*].name'). Returns the matched values as a JSON array.")]
    public static JsonNode Query(
        [Description("The JSON document.")] string json,
        [Description("A JSONPath expression, e.g. '$.store.book[?@.price < 10].title'.")] string path)
    {
        var node = ParseNode(json);
        Json.Path.JsonPath jp;
        try
        {
            jp = Json.Path.JsonPath.Parse(path);
        }
        catch (Exception ex)
        {
            throw new McpException($"invalid JSONPath: {ex.Message}");
        }

        var result = jp.Evaluate(node);
        var values = result.Matches.Select(m => m.Value?.DeepClone()).ToArray();
        return new JsonArray(values);
    }

    [McpServerTool(Name = "json_validate")]
    [Description("Validate a JSON document against a JSON Schema. Returns whether it is valid and any errors.")]
    public static ValidateResult Validate(
        [Description("The JSON document to validate.")] string json,
        [Description("The JSON Schema (draft 2020-12).")] string schema)
    {
        JsonSchema parsed;
        try
        {
            parsed = JsonSchema.FromText(schema);
        }
        catch (Exception ex)
        {
            throw new McpException($"invalid JSON Schema: {ex.Message}");
        }

        using var doc = ParseDocument(json);
        var result = parsed.Evaluate(doc.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        var errors = new List<string>();
        foreach (var detail in result.Details)
        {
            if (detail.Errors is null) continue;
            foreach (var e in detail.Errors)
                errors.Add($"{detail.InstanceLocation}: {e.Value}");
        }

        return new ValidateResult(result.IsValid, errors);
    }

    [McpServerTool(Name = "json_format")]
    [Description("Re-serialize a JSON document, pretty-printed (indented) or minified.")]
    public static string Format(
        [Description("The JSON document.")] string json,
        [Description("True to indent (default), false to minify.")] bool indent = true)
    {
        var node = ParseNode(json);
        return node?.ToJsonString(new JsonSerializerOptions { WriteIndented = indent }) ?? "null";
    }

    private static JsonNode? ParseNode(string json)
    {
        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new McpException($"invalid JSON: {ex.Message}");
        }
    }

    private static JsonDocument ParseDocument(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new McpException($"invalid JSON: {ex.Message}");
        }
    }
}

public sealed record ValidateResult(bool Valid, IReadOnlyList<string> Errors);
