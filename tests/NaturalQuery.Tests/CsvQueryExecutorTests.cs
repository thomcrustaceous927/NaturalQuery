using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NaturalQuery.Providers;

namespace NaturalQuery.Tests;

public class CsvQueryExecutorTests : IDisposable
{
    private CsvQueryExecutor? _executor;

    private CsvQueryExecutor CreateExecutor(string csv, string tableName = "data")
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        _executor = new CsvQueryExecutor(stream, NullLogger<CsvQueryExecutor>.Instance, tableName);
        return _executor;
    }

    public void Dispose()
    {
        _executor?.Dispose();
    }

    // ── Chart queries ──────────────────────────────────────────

    [Fact]
    public async Task ExecuteChartQueryAsync_GroupBy_Returns_Correct_DataPoints()
    {
        var csv = "name,category,price\nWidget,Hardware,9.99\nGadget,Electronics,24.99\nBolt,Hardware,4.99";
        var executor = CreateExecutor(csv);

        var result = await executor.ExecuteChartQueryAsync(
            "SELECT category, COUNT(*) FROM data GROUP BY category ORDER BY category");

        result.Should().HaveCount(2);
        result.Should().Contain(dp => dp.Label == "Electronics" && dp.Value == 1);
        result.Should().Contain(dp => dp.Label == "Hardware" && dp.Value == 2);
    }

    // ── Table queries ──────────────────────────────────────────

    [Fact]
    public async Task ExecuteTableQueryAsync_SelectAll_Returns_AllRows()
    {
        var csv = "id,name,score\n1,Alice,95\n2,Bob,87\n3,Carol,92";
        var executor = CreateExecutor(csv);

        var result = await executor.ExecuteTableQueryAsync("SELECT * FROM data ORDER BY id");

        result.Should().HaveCount(3);
        result[0]["name"].Should().Be("Alice");
        result[1]["name"].Should().Be("Bob");
        result[2]["name"].Should().Be("Carol");
    }

    // ── Type detection ─────────────────────────────────────────

    [Fact]
    public void GetDiscoveredSchema_Detects_Int_Double_And_String_Types()
    {
        var csv = "count,rating,name\n10,4.5,Alice\n20,3.2,Bob\n30,4.8,Carol";
        var executor = CreateExecutor(csv);

        var schema = executor.GetDiscoveredSchema();

        schema.Name.Should().Be("data");
        schema.Columns.Should().HaveCount(3);
        schema.Columns[0].Name.Should().Be("count");
        schema.Columns[0].Type.Should().Be("int");
        schema.Columns[1].Name.Should().Be("rating");
        schema.Columns[1].Type.Should().Be("double");
        schema.Columns[2].Name.Should().Be("name");
        schema.Columns[2].Type.Should().Be("string");
    }

    [Fact]
    public void GetDiscoveredSchema_Returns_Correct_ColumnNames()
    {
        var csv = "First Name,Last Name,Age\nJohn,Doe,30";
        var executor = CreateExecutor(csv);

        var schema = executor.GetDiscoveredSchema();

        schema.Columns.Should().HaveCount(3);
        schema.Columns[0].Name.Should().Be("first_name");
        schema.Columns[1].Name.Should().Be("last_name");
        schema.Columns[2].Name.Should().Be("age");
    }

    // ── Edge cases ─────────────────────────────────────────────

    [Fact]
    public void GetDiscoveredSchema_EmptyCsv_HeaderOnly_DoesNotCrash()
    {
        var csv = "id,name,value";
        var executor = CreateExecutor(csv);

        var schema = executor.GetDiscoveredSchema();

        schema.Should().NotBeNull();
        schema.Columns.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteTableQueryAsync_QuotedFields_WithCommas_ParsedCorrectly()
    {
        var csv = "name,address,city\n\"Doe, John\",\"123 Main St, Apt 4\",Springfield\nJane,456 Oak Ave,Portland";
        var executor = CreateExecutor(csv);

        var result = await executor.ExecuteTableQueryAsync("SELECT * FROM data ORDER BY city");

        result.Should().HaveCount(2);
        result[0]["name"].Should().Be("Jane");
        result[1]["name"].Should().Be("Doe, John");
        result[1]["address"].Should().Be("123 Main St, Apt 4");
    }

    [Fact]
    public async Task ExecuteTableQueryAsync_WhereFilter_Works_On_LoadedData()
    {
        var csv = "product,category,quantity\nApple,Fruit,50\nCarrot,Vegetable,30\nBanana,Fruit,40\nBroccoli,Vegetable,25";
        var executor = CreateExecutor(csv);

        var result = await executor.ExecuteTableQueryAsync(
            "SELECT product, quantity FROM data WHERE category = 'Fruit' ORDER BY product");

        result.Should().HaveCount(2);
        result[0]["product"].Should().Be("Apple");
        result[1]["product"].Should().Be("Banana");
    }

    // ── Custom table name ──────────────────────────────────────

    [Fact]
    public async Task ExecuteTableQueryAsync_CustomTableName_Works()
    {
        var csv = "id,value\n1,100\n2,200";
        var executor = CreateExecutor(csv, "sales");

        var result = await executor.ExecuteTableQueryAsync("SELECT * FROM sales");

        result.Should().HaveCount(2);
    }
}
