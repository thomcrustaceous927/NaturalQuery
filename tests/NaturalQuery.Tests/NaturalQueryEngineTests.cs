using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NaturalQuery.Caching;
using NaturalQuery.Diagnostics;
using NaturalQuery.Models;
using NaturalQuery.Providers;
using NaturalQuery.RateLimiting;

namespace NaturalQuery.Tests;

public class NaturalQueryEngineTests
{
    private readonly Mock<ILlmProvider> _llmMock = new();
    private readonly Mock<IQueryExecutor> _executorMock = new();
    private readonly Mock<IQueryCache> _cacheMock = new();
    private readonly Mock<IRateLimiter> _rateLimiterMock = new();
    private readonly Mock<IErrorHandler> _errorHandlerMock = new();

    private NaturalQueryEngine CreateEngine(
        Action<NaturalQueryOptions>? configure = null,
        IQueryCache? cache = null,
        IRateLimiter? rateLimiter = null,
        IErrorHandler? errorHandler = null)
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
            NullLogger<NaturalQueryEngine>.Instance,
            cache,
            rateLimiter,
            errorHandler);
    }

    // ==================== BuildSystemPrompt ====================

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

    // ==================== ParseResponse ====================

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

    // ==================== AskAsync ====================

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

    // ==================== AskAsync with ConversationContext ====================

    [Fact]
    public async Task AskAsync_Should_Pass_Conversation_Context_To_LLM()
    {
        var engine = CreateEngine(o => { o.TenantIdColumn = null; o.TenantIdPlaceholder = null; });
        var context = new ConversationContext();
        context.AddTurn("previous question", "SELECT * FROM users");

        _llmMock.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse(
                """{"sql":"SELECT * FROM users WHERE status = 'active'","chartType":"table","title":"T","description":"D"}""",
                100));

        _executorMock.Setup(x => x.ExecuteTableQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Dictionary<string, string>>());

        await engine.AskAsync("now filter by active", context: context);

        _llmMock.Verify(x => x.GenerateAsync(
            It.IsAny<string>(),
            It.Is<string>(prompt => prompt.Contains("previous question") && prompt.Contains("follow-up")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AskAsync_Should_Add_Turn_To_Context_After_Success()
    {
        var engine = CreateEngine(o => { o.TenantIdColumn = null; o.TenantIdPlaceholder = null; });
        var context = new ConversationContext();

        _llmMock.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse(
                """{"sql":"SELECT * FROM users","chartType":"table","title":"T","description":"D"}""",
                100));

        _executorMock.Setup(x => x.ExecuteTableQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Dictionary<string, string>>());

        await engine.AskAsync("list users", context: context);

        context.Turns.Should().HaveCount(1);
        context.Turns[0].Question.Should().Be("list users");
    }

    // ==================== Cache integration ====================

    [Fact]
    public async Task AskAsync_Should_Return_Cached_Result_On_Cache_Hit()
    {
        var cachedResult = new QueryResult
        {
            Sql = "SELECT * FROM users",
            ChartType = "table",
            Title = "Cached",
            Description = "From cache"
        };

        _cacheMock.Setup(x => x.GetAsync("list users", "t1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResult);

        var engine = CreateEngine(cache: _cacheMock.Object);

        var result = await engine.AskAsync("list users", "t1");

        result.Title.Should().Be("Cached");
        _llmMock.Verify(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AskAsync_Should_Call_LLM_On_Cache_Miss_And_Store_Result()
    {
        _cacheMock.Setup(x => x.GetAsync("list users", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((QueryResult?)null);

        var engine = CreateEngine(
            o => { o.TenantIdColumn = null; o.TenantIdPlaceholder = null; },
            cache: _cacheMock.Object);

        _llmMock.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse(
                """{"sql":"SELECT * FROM users","chartType":"table","title":"T","description":"D"}""",
                100));

        _executorMock.Setup(x => x.ExecuteTableQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Dictionary<string, string>>());

        await engine.AskAsync("list users");

        _llmMock.Verify(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.SetAsync("list users", null, It.IsAny<QueryResult>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ==================== Rate limiting ====================

    [Fact]
    public async Task AskAsync_Should_Pass_When_Rate_Limiter_Allows()
    {
        _rateLimiterMock.Setup(x => x.IsAllowedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var engine = CreateEngine(
            o => { o.TenantIdColumn = null; o.TenantIdPlaceholder = null; },
            rateLimiter: _rateLimiterMock.Object);

        _llmMock.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse(
                """{"sql":"SELECT * FROM users","chartType":"table","title":"T","description":"D"}""",
                100));

        _executorMock.Setup(x => x.ExecuteTableQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Dictionary<string, string>>());

        var result = await engine.AskAsync("list users");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task AskAsync_Should_Throw_When_Rate_Limit_Exceeded()
    {
        _rateLimiterMock.Setup(x => x.IsAllowedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var engine = CreateEngine(rateLimiter: _rateLimiterMock.Object);

        var act = () => engine.AskAsync("list users", "t1");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Rate limit*");
    }

    // ==================== Error handler ====================

    [Fact]
    public async Task AskAsync_Should_Call_Error_Handler_On_Rate_Limit()
    {
        _rateLimiterMock.Setup(x => x.IsAllowedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var engine = CreateEngine(
            rateLimiter: _rateLimiterMock.Object,
            errorHandler: _errorHandlerMock.Object);

        try { await engine.AskAsync("list users", "t1"); } catch { }

        _errorHandlerMock.Verify(x => x.HandleAsync(
            It.Is<NaturalQueryError>(e => e.ErrorType == "rate_limit"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ==================== ExplainAsync ====================

    [Fact]
    public async Task ExplainAsync_Should_Return_LLM_Explanation()
    {
        _llmMock.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse("This query selects all users from the database.", 50));

        var engine = CreateEngine();
        var explanation = await engine.ExplainAsync("SELECT * FROM users");

        explanation.Should().Be("This query selects all users from the database.");
    }

    [Fact]
    public async Task ExplainAsync_Should_Trim_Response()
    {
        _llmMock.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse("  explanation with whitespace  ", 50));

        var engine = CreateEngine();
        var explanation = await engine.ExplainAsync("SELECT 1");

        explanation.Should().Be("explanation with whitespace");
    }

    // ==================== SuggestQuestionsAsync ====================

    [Fact]
    public async Task SuggestQuestionsAsync_Should_Return_Suggestions()
    {
        _llmMock.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse("""["How many users?","Active users?","Users by status?"]""", 50));

        var engine = CreateEngine();
        var suggestions = await engine.SuggestQuestionsAsync(3);

        suggestions.Should().HaveCount(3);
        suggestions[0].Should().Be("How many users?");
    }

    [Fact]
    public async Task SuggestQuestionsAsync_Should_Handle_Markdown_Fences()
    {
        var response = "```json\n[\"Question 1\",\"Question 2\"]\n```";
        _llmMock.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse(response, 50));

        var engine = CreateEngine();
        var suggestions = await engine.SuggestQuestionsAsync(2);

        suggestions.Should().HaveCount(2);
    }

    [Fact]
    public async Task SuggestQuestionsAsync_Should_Return_Empty_On_Invalid_Json()
    {
        _llmMock.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse("not json at all", 50));

        var engine = CreateEngine();
        var suggestions = await engine.SuggestQuestionsAsync();

        suggestions.Should().BeEmpty();
    }

    [Fact]
    public async Task SuggestQuestionsAsync_Should_Limit_Results_To_Requested_Count()
    {
        _llmMock.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse("""["a","b","c","d","e","f","g"]""", 50));

        var engine = CreateEngine();
        var suggestions = await engine.SuggestQuestionsAsync(3);

        suggestions.Should().HaveCount(3);
    }

    // ==================== Auto-retry ====================

    [Fact]
    public async Task AskAsync_Should_Not_Retry_When_MaxRetries_Is_Zero()
    {
        var engine = CreateEngine(o =>
        {
            o.TenantIdColumn = null;
            o.TenantIdPlaceholder = null;
            o.MaxRetries = 0;
        });

        _llmMock.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse(
                """{"sql":"SELECT * FROM users","chartType":"table","title":"T","description":"D"}""",
                100));

        _executorMock.Setup(x => x.ExecuteTableQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Query failed: unknown column 'xyz'"));

        var act = () => engine.AskAsync("list users");

        await act.Should().ThrowAsync<Exception>().WithMessage("*unknown column*");

        // LLM should only be called once (no retry)
        _llmMock.Verify(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AskAsync_Should_Retry_And_Succeed_On_Second_Attempt()
    {
        var engine = CreateEngine(o =>
        {
            o.TenantIdColumn = null;
            o.TenantIdPlaceholder = null;
            o.MaxRetries = 2;
        });

        // First call: returns SQL that will fail execution
        // Second call (repair): returns fixed SQL
        var callCount = 0;
        _llmMock.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                    return new LlmResponse(
                        """{"sql":"SELECT xyz FROM users","chartType":"table","title":"T","description":"D"}""",
                        100);
                return new LlmResponse(
                    """{"sql":"SELECT name FROM users","chartType":"table","title":"Fixed","description":"D"}""",
                    80);
            });

        var execCallCount = 0;
        _executorMock.Setup(x => x.ExecuteTableQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                execCallCount++;
                if (execCallCount == 1)
                    throw new Exception("Query failed: unknown column 'xyz'");
                return new List<Dictionary<string, string>>
                {
                    new() { ["name"] = "Alice" }
                };
            });

        var result = await engine.AskAsync("list users");

        result.Title.Should().Be("Fixed");
        result.TableData.Should().HaveCount(1);

        // LLM called twice: initial + one retry
        _llmMock.Verify(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task AskAsync_Should_Throw_Last_Error_When_All_Retries_Exhausted()
    {
        var engine = CreateEngine(o =>
        {
            o.TenantIdColumn = null;
            o.TenantIdPlaceholder = null;
            o.MaxRetries = 2;
        });

        _llmMock.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse(
                """{"sql":"SELECT * FROM users","chartType":"table","title":"T","description":"D"}""",
                100));

        _executorMock.Setup(x => x.ExecuteTableQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Query failed: persistent error"));

        var act = () => engine.AskAsync("list users");

        await act.Should().ThrowAsync<Exception>().WithMessage("*persistent error*");

        // LLM called 3 times: initial + 2 retries
        _llmMock.Verify(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task AskAsync_Retry_Prompt_Should_Contain_Error_Message()
    {
        var engine = CreateEngine(o =>
        {
            o.TenantIdColumn = null;
            o.TenantIdPlaceholder = null;
            o.MaxRetries = 1;
        });

        var llmCalls = new List<string>();
        _llmMock.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((sys, user, _) => llmCalls.Add(user))
            .ReturnsAsync(new LlmResponse(
                """{"sql":"SELECT * FROM users","chartType":"table","title":"T","description":"D"}""",
                100));

        _executorMock.Setup(x => x.ExecuteTableQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Query failed: column 'abc' not found"));

        try { await engine.AskAsync("list users"); } catch { }

        // Second LLM call should be the repair prompt containing the error
        llmCalls.Should().HaveCount(2);
        llmCalls[1].Should().Contain("column 'abc' not found");
        llmCalls[1].Should().Contain("list users");
        llmCalls[1].Should().Contain("fix the SQL");
    }
}
