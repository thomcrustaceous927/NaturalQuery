namespace NaturalQuery.Models;

/// <summary>
/// Supported chart/visualization types that the LLM can recommend.
/// </summary>
public static class ChartType
{
    public const string Line = "line";
    public const string Bar = "bar";
    public const string BarHorizontal = "bar_horizontal";
    public const string Pie = "pie";
    public const string Donut = "donut";
    public const string Area = "area";
    public const string Metric = "metric";
    public const string Table = "table";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Line, Bar, BarHorizontal, Pie, Donut, Area, Metric, Table
    };

    public static bool IsValid(string type) => All.Contains(type);
}
