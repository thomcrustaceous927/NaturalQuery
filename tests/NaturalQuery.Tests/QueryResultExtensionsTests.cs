using System.Text.Json;
using FluentAssertions;
using NaturalQuery.Extensions;
using NaturalQuery.Models;

namespace NaturalQuery.Tests;

public class QueryResultExtensionsTests
{
    // ── ToCsv ──────────────────────────────────────────────────

    [Fact]
    public void ToCsv_ChartData_Outputs_LabelValue_Format()
    {
        var result = new QueryResult
        {
            ChartData = new List<DataPoint>
            {
                new("Hardware", 3),
                new("Electronics", 2)
            }
        };

        var csv = result.ToCsv();

        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().Be("Label,Value");
        lines[1].Should().Be("Hardware,3");
        lines[2].Should().Be("Electronics,2");
    }

    [Fact]
    public void ToCsv_TableData_Outputs_AllColumns()
    {
        var result = new QueryResult
        {
            TableData = new List<Dictionary<string, string>>
            {
                new() { { "id", "1" }, { "name", "Alice" }, { "score", "95" } },
                new() { { "id", "2" }, { "name", "Bob" }, { "score", "87" } }
            }
        };

        var csv = result.ToCsv();

        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().Be("id,name,score");
        lines[1].Should().Be("1,Alice,95");
        lines[2].Should().Be("2,Bob,87");
    }

    [Fact]
    public void ToCsv_Escapes_Fields_With_Commas_And_Quotes()
    {
        var result = new QueryResult
        {
            TableData = new List<Dictionary<string, string>>
            {
                new() { { "name", "Doe, John" }, { "note", "said \"hello\"" } }
            }
        };

        var csv = result.ToCsv();

        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines[1].Should().Contain("\"Doe, John\"");
        lines[1].Should().Contain("\"said \"\"hello\"\"\"");
    }

    [Fact]
    public void ToCsv_EmptyResult_Returns_EmptyString()
    {
        var result = new QueryResult();

        var csv = result.ToCsv();

        csv.Should().BeEmpty();
    }

    // ── ToJson ─────────────────────────────────────────────────

    [Fact]
    public void ToJson_Includes_Metadata_And_Data()
    {
        var result = new QueryResult
        {
            Title = "Sales Report",
            Description = "Monthly sales",
            ChartType = "bar",
            Sql = "SELECT * FROM sales",
            Suggestions = new List<string> { "Show by region" },
            TokensUsed = 150,
            ElapsedMs = 200,
            ChartData = new List<DataPoint>
            {
                new("Jan", 100),
                new("Feb", 200)
            }
        };

        var json = result.ToJson();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("title").GetString().Should().Be("Sales Report");
        root.GetProperty("description").GetString().Should().Be("Monthly sales");
        root.GetProperty("chartType").GetString().Should().Be("bar");
        root.GetProperty("sql").GetString().Should().Be("SELECT * FROM sales");
        root.GetProperty("suggestions").GetArrayLength().Should().Be(1);
        root.GetProperty("tokensUsed").GetInt32().Should().Be(150);
        root.GetProperty("elapsedMs").GetInt64().Should().Be(200);
        root.GetProperty("data").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public void ToJson_Indented_Contains_Newlines()
    {
        var result = new QueryResult
        {
            Title = "Test",
            ChartData = new List<DataPoint> { new("A", 1) }
        };

        var json = result.ToJson(indented: true);

        json.Should().Contain("\n");
    }

    [Fact]
    public void ToJson_NotIndented_Has_No_Indentation()
    {
        var result = new QueryResult
        {
            Title = "Test",
            ChartData = new List<DataPoint> { new("A", 1) }
        };

        var json = result.ToJson(indented: false);

        json.Should().NotContain("\n");
    }
}
