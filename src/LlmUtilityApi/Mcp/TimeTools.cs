using System.ComponentModel;
using System.Globalization;
using Cronos;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace LlmUtilityApi.Mcp;

[McpServerToolType]
public static class TimeTools
{
    [McpServerTool(Name = "time_now")]
    [Description("Current date/time as UTC ISO-8601 + Unix seconds. Optionally also rendered in an IANA timezone.")]
    public static NowResult Now(
        [Description("IANA timezone, e.g. 'Europe/Stockholm' (optional).")] string? timezone = null)
    {
        var utc = DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(timezone))
            return new NowResult(utc.ToString("o"), utc.ToUnixTimeSeconds(), null, null);

        var tz = ResolveTz(timezone);
        var local = TimeZoneInfo.ConvertTime(utc, tz);
        return new NowResult(utc.ToString("o"), utc.ToUnixTimeSeconds(), tz.Id, local.ToString("o"));
    }

    [McpServerTool(Name = "time_add")]
    [Description("Add a duration to an ISO-8601 timestamp. Returns the resulting timestamp (ISO-8601).")]
    public static string Add(
        [Description("Base timestamp, ISO-8601, e.g. '2026-06-28T10:00:00Z'.")] string timestamp,
        [Description("Days to add (may be negative).")] int days = 0,
        [Description("Hours to add (may be negative).")] int hours = 0,
        [Description("Minutes to add (may be negative).")] int minutes = 0,
        [Description("Seconds to add (may be negative).")] int seconds = 0)
    {
        var ts = Parse(timestamp);
        return ts.AddDays(days).AddHours(hours).AddMinutes(minutes).AddSeconds(seconds).ToString("o");
    }

    [McpServerTool(Name = "time_diff")]
    [Description("Difference between two ISO-8601 timestamps (to - from), in seconds and as a human string.")]
    public static DiffResult Diff(
        [Description("Start timestamp, ISO-8601.")] string from,
        [Description("End timestamp, ISO-8601.")] string to)
    {
        var span = Parse(to) - Parse(from);
        return new DiffResult(span.TotalSeconds, Humanize(span));
    }

    [McpServerTool(Name = "time_parse")]
    [Description("Parse a date/time string into UTC ISO-8601 + Unix seconds.")]
    public static ParseResult ParseTimestamp(
        [Description("A date/time string, e.g. '28 June 2026 10:00 +02:00'.")] string text)
    {
        var ts = Parse(text);
        return new ParseResult(ts.ToUniversalTime().ToString("o"), ts.ToUnixTimeSeconds());
    }

    [McpServerTool(Name = "time_format")]
    [Description("Format an ISO-8601 timestamp with a .NET format string, optionally in an IANA timezone.")]
    public static string Format(
        [Description("Timestamp, ISO-8601.")] string timestamp,
        [Description("A .NET format string, e.g. 'yyyy-MM-dd HH:mm' or 'dddd, dd MMMM yyyy'.")] string format,
        [Description("IANA timezone to render in (optional).")] string? timezone = null)
    {
        var ts = Parse(timestamp);
        if (!string.IsNullOrWhiteSpace(timezone))
            ts = TimeZoneInfo.ConvertTime(ts, ResolveTz(timezone));
        try
        {
            return ts.ToString(format, CultureInfo.InvariantCulture);
        }
        catch (FormatException ex)
        {
            throw new McpException($"invalid format string: {ex.Message}");
        }
    }

    [McpServerTool(Name = "time_relative")]
    [Description("Human relative description of an ISO-8601 timestamp versus now, e.g. 'in 3 days' or '2 hours ago'.")]
    public static string Relative(
        [Description("Timestamp, ISO-8601.")] string timestamp)
    {
        var span = Parse(timestamp) - DateTimeOffset.UtcNow;
        var human = Humanize(span < TimeSpan.Zero ? -span : span);
        return span >= TimeSpan.Zero ? $"in {human}" : $"{human} ago";
    }

    [McpServerTool(Name = "cron_next")]
    [Description("Next occurrence(s) of a cron expression (5-field, or 6-field with seconds). Returns ISO-8601 timestamps.")]
    public static IReadOnlyList<string> CronNext(
        [Description("Cron expression, e.g. '0 9 * * 1-5' (weekdays 09:00).")] string cron,
        [Description("How many upcoming occurrences to return (default 1, max 100).")] int count = 1,
        [Description("IANA timezone the schedule is evaluated in (default UTC).")] string? timezone = null)
    {
        var tz = string.IsNullOrWhiteSpace(timezone) ? TimeZoneInfo.Utc : ResolveTz(timezone);
        var fields = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var fmt = fields.Length >= 6 ? CronFormat.IncludeSeconds : CronFormat.Standard;

        CronExpression expr;
        try
        {
            expr = CronExpression.Parse(cron, fmt);
        }
        catch (CronFormatException ex)
        {
            throw new McpException($"invalid cron expression: {ex.Message}");
        }

        var results = new List<string>();
        var from = DateTimeOffset.UtcNow;
        for (var i = 0; i < Math.Clamp(count, 1, 100); i++)
        {
            var next = expr.GetNextOccurrence(from, tz);
            if (next is null) break;
            results.Add(next.Value.ToString("o"));
            from = next.Value;
        }

        return results;
    }

    private static DateTimeOffset Parse(string text)
    {
        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var ts))
            return ts;
        throw new McpException($"could not parse timestamp '{text}'");
    }

    private static TimeZoneInfo ResolveTz(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            throw new McpException($"unknown timezone '{id}' (use an IANA id like 'Europe/Stockholm')");
        }
    }

    private static string Humanize(TimeSpan span)
    {
        if (span.TotalDays >= 1) return $"{(int) span.TotalDays}d {span.Hours}h";
        if (span.TotalHours >= 1) return $"{(int) span.TotalHours}h {span.Minutes}m";
        if (span.TotalMinutes >= 1) return $"{(int) span.TotalMinutes}m {span.Seconds}s";
        return $"{(int) span.TotalSeconds}s";
    }
}

public sealed record NowResult(string Utc, long Unix, string? Timezone, string? Local);

public sealed record DiffResult(double TotalSeconds, string Human);

public sealed record ParseResult(string Utc, long Unix);
