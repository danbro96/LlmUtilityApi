using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using NCalc;
using UnitsNet;

namespace LlmUtilityApi.Mcp;

[McpServerToolType]
public static class MathTools
{
    [McpServerTool(Name = "math_eval")]
    [Description("Evaluate a mathematical expression exactly (decimal). Supports + - * / %, parentheses, " +
        "comparisons, and functions like Abs, Sqrt, Pow, Round, Min, Max, Sin, Cos, Log.")]
    public static MathResult Eval(
        [Description("The expression, e.g. '(2 + 3) * 4' or 'Sqrt(2)'.")] string expression)
    {
        try
        {
            var value = new Expression(expression, ExpressionOptions.DecimalAsDefault).Evaluate();
            return new MathResult(expression, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
        }
        catch (Exception ex)
        {
            throw new McpException($"could not evaluate expression: {ex.Message}");
        }
    }

    [McpServerTool(Name = "convert_unit")]
    [Description("Convert a value between units of the same physical quantity, e.g. quantity 'Length' " +
        "from 'km' to 'mi', or quantity 'Mass' from 'kg' to 'lb'.")]
    public static UnitResult ConvertUnit(
        [Description("The numeric value to convert.")] double value,
        [Description("The quantity type, e.g. 'Length', 'Mass', 'Duration', 'Temperature'.")] string quantity,
        [Description("Source unit abbreviation, e.g. 'km'.")] string from,
        [Description("Target unit abbreviation, e.g. 'mi'.")] string to)
    {
        try
        {
            var result = UnitConverter.ConvertByAbbreviation(value, quantity, from, to);
            return new UnitResult(value, from, result, to, quantity);
        }
        catch (Exception ex)
        {
            throw new McpException($"could not convert {value} {from}->{to} ({quantity}): {ex.Message}");
        }
    }
}

public sealed record MathResult(string Expression, string Result);

public sealed record UnitResult(double Input, string FromUnit, double Output, string ToUnit, string Quantity);
