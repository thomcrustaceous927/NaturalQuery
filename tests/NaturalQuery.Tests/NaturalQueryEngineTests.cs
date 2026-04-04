using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NaturalQuery.Models;
using NaturalQuery.Providers;

namespace NaturalQuery.Tests;

public class NaturalQueryEngineTests
{
    private readonly Mock<ILlmProvider> _llmMock = new();
    private readonly Mock<IQueryExecutor> _executorMock = new();

    private NaturalQueryEngine CreateEngine(Action<NaturalQueryOptions>? configure = null)
    {
        var options = new NaturalQueryOptions
        {
            Tables = new List<TableSchema>
            {
                new("users", new[]
                {
                    new ColumnDef("id", "string"),
                    new ColumnDef("name", "string"),
                    new ColumnDef("status", "string"),
                })
            },
            TenantIdColumn = "tenant_id",
            TenantIdPlaceholder = "{TENANT_ID}"
        };

        configure?.Invoke(options);

        return new NaturalQueryEngine(
            _llmMock.Object,
            _executorMock.Object,
            Options.Create(options),
            NullLogger<NaturalQueryEngine>.Instance);
    }

    [Fact]
    public void BuildSystemPrompt_Should_Include_Table_Schema()
    {
        var engine = CreateEngine();
        var prompt = engine.BuildSystemPrompt();

        prompt.Should().Contain("users");
        prompt.Should().Contain("id (string)");
        prompt.Should().Contain("name (string)");
        prompt.Should().Contain("status (string)");
    }

    [Fact]
    public void BuildSystemPrompt_Should_Include_Tenant_Rule()
    {
        var engine = CreateEngine();
        var prompt = engine.BuildSystemPrompt();

        prompt.Should().Contain("tenant_id");
        prompt.Should().Contain("{TENANT_ID}");
    }

    [Fact]
    public void BuildSystemPrompt_Should_Include_Chart_Types()
    {
        var engine = CreateEngine();
        var prompt = engine.BuildSystemPrompt();

        prompt.Should().Contain("line");
        prompt.Should().Contain("bar");
        prompt.Should().Contain("pie");
        prompt.Should().Contain("table");
    }

    [Fact]
    public void BuildSystemPrompt_With_Custom_Prompt_Should_Replace_Schema_Placeholder()
    {
        var engine = CreateEngine(o =>
        {
            o.CustomSystemPrompt = "You are a helper. Schema: {TABLES_SCHEMA} End.";
        });
        var prompt = engine.BuildSystemPrompt();

        prompt.Should().Contain("users");
        prompt.Should().Contain("You are a helper");
        prompt.Should().NotContain("{TABLES_SCHEMA}");
    }

    [Fact]
    public void BuildSystemPrompt_Should_Include_Additional_Rules()
    {
        var engine = CreateEngine(o =>
        {
            o.AdditionalRules = new List<string> { "Always use UTC dates" };
        });
        var prompt = engine.BuildSystemPrompt();

        prompt.Should().Contain("Always use UTC dates");
    }

    [Fact]
    public void ParseResponse_Should_Extract_All_Fields()
    {
        var json = """
        {"sql":"SELECT name AS label, COUNT(*) AS value FROM users","chartType":"bar","title":"Users","description":"All users","suggestions":["By status","By date"]}
        """;

        var result = NaturalQueryEngine.ParseResponse(new LlmResponse(json, 500));

        result.Sql.Should().Be("SELECT name AS label, COUNT(*) AS value FROM users");
        result.ChartType.Should().Be("bar");
        result.Title.Should().Be("Users");
        result.Description.Should().Be("All users");
        result.Suggestions.Should().HaveCount(2);
        result.TokensUsed.Should().Be(500);
    }

    [Fact]
    public void ParseResponse_Should_Strip_Markdown_Fences()
    {
        var json = """
        ```json
        {"sql":"SELECT * FROM users","chartType":"table","title":"T","description":"D","suggestions":[]}
        ```
        """;

        var result = NaturalQueryEngine.ParseResponse(new LlmResponse(json, 100));
        result.Sql.Should().Be("SELECT * FROM users");
    }

    [Fact]
    public void ParseResponse_Should_Default_To_Table_For_Invalid_ChartType()
    {
        var json = """{"sql":"SELECT 1","chartType":"invalid_type","title":"T","description":"D"}""";

        var result = NaturalQueryEngine.ParseResponse(new LlmResponse(json, 100));
        result.ChartType.Should().Be("table");
    }

    [Fact]
    public void ParseResponse_Should_Reject_Missing_Sql()
    {
        var json = """{"chartType":"bar","title":"T","description":"D"}""";

        var act = () => NaturalQueryEngine.ParseResponse(new LlmResponse(json, 100));
        act.Should().Throw<InvalidOperationException>().WithMessage("*sql*");
    }

    [Fact]
    public void ParseResponse_Should_Reject_Delete_In_Sql()
    {
        var json = """{"sql":"DELETE FROM users","chartType":"table","title":"T","description":"D"}""";

        var act = () => NaturalQueryEngine.ParseResponse(new LlmResponse(json, 100));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ParseResponse_Should_Allow_With_CTE()
    {
        var json = """{"sql":"WITH latest AS (SELECT * FROM users) SELECT * FROM latest WHERE rn = 1","chartType":"table","title":"T","description":"D"}""";

        var result = NaturalQueryEngine.ParseResponse(new LlmResponse(json, 100));
        result.Sql.Should().StartWith("WITH");
    }

    [Fact]
    public void ParseResponse_Should_Limit_Suggestions_To_Three()
    {
        var json = """{"sql":"SELECT 1","chartType":"table","title":"T","description":"D","suggestions":["a","b","c","d","e"]}""";

        var result = NaturalQueryEngine.ParseResponse(new LlmResponse(json, 100));
        result.Suggestions.Should().HaveCount(3);
    }

    [Fact]
    public void ParseResponse_Should_Handle_Error_Response()
    {
        var json = """{"error":true,"message":"Cannot understand the question"}""";

        var act = () => NaturalQueryEngine.ParseResponse(new LlmResponse(json, 100));
        act.Should().Throw<InvalidOperationException>().WithMessage("Cannot understand*");
    }

    [Fact]
    public async Task AskAsync_Should_Replace_Tenant_Placeholder()
    {
        var engine = CreateEngine();

        _llmMock.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse(
                """{"sql":"SELECT * FROM users WHERE tenant_id = '{TENANT_ID}'","chartType":"table","title":"T","description":"D"}""",
                100));

        _executorMock.Setup(x => x.ExecuteTableQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Dictionary<string, string>>());

        var result = await engine.AskAsync("list all users", "my-tenant-123");

        _executorMock.Verify(x => x.ExecuteTableQueryAsync(
            It.Is<string>(sql => sql.Contains("my-tenant-123") && !sql.Contains("{TENANT_ID}")),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task AskAsync_Should_Execute_Chart_Query_For_Non_Table()
    {
        var engine = CreateEngine(o => { o.TenantIdColumn = null; o.TenantIdPlaceholder = null; });

        _llmMock.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse(
                """{"sql":"SELECT status AS label, COUNT(*) AS value FROM users GROUP BY status","chartType":"pie","title":"T","description":"D"}""",
                100));

        _executorMock.Setup(x => x.ExecuteChartQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DataPoint> { new("Active", 10), new("Inactive", 5) });

        var result = await engine.AskAsync("users by status");

        result.ChartType.Should().Be("pie");
        result.ChartData.Should().HaveCount(2);
        result.TableData.Should().BeNull();
    }

    [Fact]
    public async Task AskAsync_Should_Execute_Table_Query_For_Table_Type()
    {
        var engine = CreateEngine(o => { o.TenantIdColumn = null; o.TenantIdPlaceholder = null; });

        _llmMock.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse(
                """{"sql":"SELECT name, status FROM users","chartType":"table","title":"T","description":"D"}""",
                100));

        _executorMock.Setup(x => x.ExecuteTableQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Dictionary<string, string>>
            {
                new() { ["name"] = "Alice", ["status"] = "Active" }
            });

        var result = await engine.AskAsync("list all users");

        result.ChartType.Should().Be("table");
        result.TableData.Should().HaveCount(1);
        result.ChartData.Should().BeNull();
    }
}
