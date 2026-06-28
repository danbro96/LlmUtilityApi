using System.ComponentModel;
using System.Text.RegularExpressions;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace LlmUtilityApi.Mcp;

[McpServerToolType]
public static partial class RandomTools
{
    [McpServerTool(Name = "random")]
    [Description("Generate random integers in [min, max] (inclusive). Optionally several, optionally seeded for reproducibility.")]
    public static IReadOnlyList<int> Random(
        [Description("Inclusive lower bound.")] int min,
        [Description("Inclusive upper bound.")] int max,
        [Description("How many to generate (default 1).")] int count = 1,
        [Description("Seed for reproducible output (optional).")] int? seed = null)
    {
        if (min > max) throw new McpException("min must be <= max");
        if (count is < 1 or > 10_000) throw new McpException("count must be between 1 and 10000");
        var rng = Rng(seed);
        return Enumerable.Range(0, count).Select(_ => rng.Next(min, max == int.MaxValue ? max : max + 1)).ToList();
    }

    [McpServerTool(Name = "dice")]
    [Description("Roll dice in NdM(+/-K) notation, e.g. '2d6+1' or '4d8'. Returns the individual rolls and the total.")]
    public static DiceResult Dice(
        [Description("Dice notation, e.g. '3d6+2'.")] string notation,
        [Description("Seed for reproducible output (optional).")] int? seed = null)
    {
        var m = DiceNotation().Match(notation.Replace(" ", string.Empty));
        if (!m.Success) throw new McpException("notation must look like '2d6', '1d20+5', or '4d8-1'");

        var n = int.Parse(m.Groups[1].Value);
        var sides = int.Parse(m.Groups[2].Value);
        var modifier = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0;
        if (n is < 1 or > 1000 || sides < 1) throw new McpException("dice count must be 1..1000 and sides >= 1");

        var rng = Rng(seed);
        var rolls = Enumerable.Range(0, n).Select(_ => rng.Next(1, sides + 1)).ToList();
        return new DiceResult(rolls, rolls.Sum() + modifier);
    }

    [McpServerTool(Name = "shuffle")]
    [Description("Return the items in a random order (Fisher–Yates). Optionally seeded.")]
    public static IReadOnlyList<string> Shuffle(
        [Description("The items to shuffle.")] string[] items,
        [Description("Seed for reproducible output (optional).")] int? seed = null)
    {
        var copy = (string[]) items.Clone();
        Rng(seed).Shuffle(copy);
        return copy;
    }

    [McpServerTool(Name = "sample")]
    [Description("Pick a number of items at random without replacement. Optionally seeded.")]
    public static IReadOnlyList<string> Sample(
        [Description("The items to sample from.")] string[] items,
        [Description("How many to pick.")] int count,
        [Description("Seed for reproducible output (optional).")] int? seed = null)
    {
        if (count < 0 || count > items.Length) throw new McpException("count must be between 0 and the number of items");
        var copy = (string[]) items.Clone();
        Rng(seed).Shuffle(copy);
        return copy.Take(count).ToList();
    }

    private static Random Rng(int? seed) => seed is { } s ? new Random(s) : System.Random.Shared;

    [GeneratedRegex(@"^(\d+)d(\d+)([+-]\d+)?$", RegexOptions.IgnoreCase)]
    private static partial Regex DiceNotation();
}

public sealed record DiceResult(IReadOnlyList<int> Rolls, int Total);
