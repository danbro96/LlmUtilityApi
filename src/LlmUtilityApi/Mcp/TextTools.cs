using System.ComponentModel;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Scriban;
using Scriban.Runtime;

namespace LlmUtilityApi.Mcp;

[McpServerToolType]
public static class TextTools
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

    [McpServerTool(Name = "regex_test")]
    [Description("Test whether a .NET regex matches the input. Returns whether it matched and how many matches.")]
    public static RegexTestResult RegexTest(
        [Description("The .NET regex pattern.")] string pattern,
        [Description("The input string.")] string input,
        [Description("Case-insensitive matching (default false).")] bool ignoreCase = false)
    {
        var rx = Compile(pattern, ignoreCase);
        var matches = rx.Matches(input);
        return new RegexTestResult(matches.Count > 0, matches.Count);
    }

    [McpServerTool(Name = "regex_extract")]
    [Description("Return all matches of a .NET regex against the input, each with its captured groups.")]
    public static IReadOnlyList<RegexMatchInfo> RegexExtract(
        [Description("The .NET regex pattern.")] string pattern,
        [Description("The input string.")] string input,
        [Description("Case-insensitive matching (default false).")] bool ignoreCase = false)
    {
        var rx = Compile(pattern, ignoreCase);
        try
        {
            return rx.Matches(input)
                .Select(m => new RegexMatchInfo(m.Value, m.Groups.Values.Skip(1).Select(g => g.Value).ToArray()))
                .ToList();
        }
        catch (RegexMatchTimeoutException)
        {
            throw new McpException("regex matching timed out (possible catastrophic backtracking)");
        }
    }

    [McpServerTool(Name = "regex_replace")]
    [Description("Replace all matches of a .NET regex in the input with a replacement (supports $1, ${name}).")]
    public static string RegexReplace(
        [Description("The .NET regex pattern.")] string pattern,
        [Description("The input string.")] string input,
        [Description("The replacement string.")] string replacement,
        [Description("Case-insensitive matching (default false).")] bool ignoreCase = false)
    {
        var rx = Compile(pattern, ignoreCase);
        try
        {
            return rx.Replace(input, replacement);
        }
        catch (RegexMatchTimeoutException)
        {
            throw new McpException("regex matching timed out (possible catastrophic backtracking)");
        }
    }

    [McpServerTool(Name = "diff")]
    [Description("Line-by-line diff between two texts. Each line is prefixed '+ ' (added), '- ' (removed), '~ ' (changed), or '  ' (unchanged).")]
    public static string Diff(
        [Description("The original text.")] string before,
        [Description("The updated text.")] string after)
    {
        var model = new InlineDiffBuilder(new Differ()).BuildDiffModel(before, after);
        var sb = new StringBuilder();
        foreach (var line in model.Lines)
        {
            var prefix = line.Type switch
            {
                ChangeType.Inserted => "+ ",
                ChangeType.Deleted => "- ",
                ChangeType.Modified => "~ ",
                _ => "  ",
            };
            sb.Append(prefix).AppendLine(line.Text);
        }

        return sb.ToString();
    }

    [McpServerTool(Name = "template")]
    [Description("Render a Scriban (Liquid-like) template against a JSON data object. Use {{ name }} for fields, {{ for x in items }}…{{ end }} for loops.")]
    public static string Template(
        [Description("The Scriban template text.")] string template,
        [Description("JSON object providing the template's data (optional).")] string? data = null)
    {
        Template parsed;
        try
        {
            parsed = Scriban.Template.Parse(template);
        }
        catch (Exception ex)
        {
            throw new McpException($"could not parse template: {ex.Message}");
        }

        if (parsed.HasErrors)
            throw new McpException($"template errors: {string.Join("; ", parsed.Messages)}");

        var global = new ScriptObject();
        if (!string.IsNullOrWhiteSpace(data))
        {
            JsonNode? node;
            try
            {
                node = JsonNode.Parse(data);
            }
            catch (Exception ex)
            {
                throw new McpException($"invalid JSON data: {ex.Message}");
            }

            if (ToScriban(node) is ScriptObject obj) global = obj;
        }

        var ctx = new TemplateContext();
        ctx.PushGlobal(global);
        try
        {
            return parsed.Render(ctx);
        }
        catch (Exception ex)
        {
            throw new McpException($"could not render template: {ex.Message}");
        }
    }

    private static Regex Compile(string pattern, bool ignoreCase)
    {
        try
        {
            var options = RegexOptions.CultureInvariant | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
            return new Regex(pattern, options, RegexTimeout);
        }
        catch (RegexParseException ex)
        {
            throw new McpException($"invalid regex: {ex.Message}");
        }
    }

    private static object? ToScriban(JsonNode? node) => node switch
    {
        JsonObject o => BuildObject(o),
        JsonArray a => a.Select(ToScriban).ToList(),
        JsonValue v => ToScalar(v),
        _ => null,
    };

    private static ScriptObject BuildObject(JsonObject o)
    {
        var so = new ScriptObject();
        foreach (var kv in o) so[kv.Key] = ToScriban(kv.Value);
        return so;
    }

    private static object? ToScalar(JsonValue v)
    {
        if (v.TryGetValue<bool>(out var b)) return b;
        if (v.TryGetValue<long>(out var l)) return l;
        if (v.TryGetValue<double>(out var d)) return d;
        if (v.TryGetValue<string>(out var s)) return s;
        return v.ToJsonString();
    }
}

public sealed record RegexTestResult(bool IsMatch, int Count);

public sealed record RegexMatchInfo(string Value, string[] Groups);
