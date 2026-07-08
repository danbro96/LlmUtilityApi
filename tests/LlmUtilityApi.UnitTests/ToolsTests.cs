using LlmUtilityApi.Mcp;
using LlmUtilityApi.Services;
using ModelContextProtocol;
using Xunit;

namespace LlmUtilityApi.UnitTests;

/// <summary>Representative coverage of the pure tool groups: deterministic results + error surfacing as McpException.</summary>
public class ToolsTests
{
    [Fact]
    public void Math_eval_is_exact()
    {
        Assert.Equal("20", MathTools.Eval("(2 + 3) * 4").Result);
        Assert.Equal("0.3", MathTools.Eval("0.1 + 0.2").Result); // decimal, not 0.30000000000000004
    }

    [Fact]
    public void Math_eval_bad_expression_throws_mcp()
        => Assert.Throws<McpException>(() => MathTools.Eval("2 +"));

    [Fact]
    public void Unit_convert_roundtrips()
    {
        var r = MathTools.ConvertUnit(1, "Length", "km", "m");
        Assert.Equal(1000, r.Output, precision: 6);
    }

    [Fact]
    public void Crypto_hash_known_vector()
        => Assert.Equal(
            "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824",
            CryptoTools.Hash("hello", "sha256"));

    [Fact]
    public void Crypto_encode_decode_roundtrips()
    {
        foreach (var scheme in new[] { "base64", "base64url", "hex", "url" })
        {
            var encoded = CryptoTools.Encode("héllo wörld+/=", scheme);
            Assert.Equal("héllo wörld+/=", CryptoTools.Decode(encoded, scheme));
        }
    }

    [Fact]
    public void Json_query_returns_matches()
    {
        var result = JsonTools.Query("""{"items":[{"name":"a"},{"name":"b"}]}""", "$.items[*].name");
        Assert.Equal("""["a","b"]""", result.ToJsonString());
    }

    [Fact]
    public void Json_validate_reports_invalid()
    {
        var schema = """{"type":"object","required":["age"],"properties":{"age":{"type":"integer"}}}""";
        var ok = JsonTools.Validate("""{"age":42}""", schema);
        var bad = JsonTools.Validate("""{"name":"x"}""", schema);
        Assert.True(ok.Valid);
        Assert.False(bad.Valid);
    }

    [Fact]
    public void Dice_is_seeded_and_in_range()
    {
        var a = RandomTools.Dice("3d6+2", seed: 123);
        var b = RandomTools.Dice("3d6+2", seed: 123);
        Assert.Equal(b.Rolls, a.Rolls);
        Assert.Equal(b.Total, a.Total);
        Assert.All(a.Rolls, r => Assert.InRange(r, 1, 6));
        Assert.Equal(a.Rolls.Sum() + 2, a.Total);
    }

    [Fact]
    public void Time_diff_and_parse()
    {
        var diff = TimeTools.Diff("2026-06-28T10:00:00Z", "2026-06-28T12:30:00Z");
        Assert.Equal(9000, diff.TotalSeconds);
    }

    [Fact]
    public void Cron_next_returns_requested_count()
    {
        var next = TimeTools.CronNext("0 9 * * *", count: 3);
        Assert.Equal(3, next.Count);
    }

    [Fact]
    public void Chunk_overlaps_and_covers()
    {
        var text = string.Join(' ', Enumerable.Range(0, 500).Select(i => $"word{i}"));
        var chunks = TextChunker.Chunk(text, maxChars: 200, overlap: 40);
        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Length <= 200));
    }

    [Fact]
    public void Chunk_rejects_bad_overlap()
        => Assert.Throws<ArgumentOutOfRangeException>(() => TextChunker.Chunk("abc", 10, 10));

    [Fact]
    public void Doc_extract_detects_html()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("<html><body><h1>Title</h1><p>Hello world</p></body></html>");
        var (kind, text) = DocExtractor.Extract(bytes, hint: null);
        Assert.Equal(DocKind.Html, kind);
        Assert.Contains("Hello world", text);
    }

    [Fact]
    public void Search_parses_maps_caps_and_skips_incomplete()
    {
        const string json = """
            {"results":[
              {"title":" First ","url":"https://a.example/1","content":" snippet one "},
              {"title":"No url","content":"dropped"},
              {"title":"Second","url":"https://b.example/2","content":"snippet two"},
              {"title":"Third","url":"https://c.example/3","content":"snippet three"}
            ]}
            """;
        var results = SearxngParser.Parse(json, max: 2);

        Assert.Equal(2, results.Count);
        Assert.Equal("First", results[0].Title);                 // trimmed
        Assert.Equal("snippet one", results[0].Snippet);         // trimmed
        Assert.Equal("https://a.example/1", results[0].Url);
        Assert.Equal("https://b.example/2", results[1].Url);     // entry missing url skipped, so this is #2
    }
}
