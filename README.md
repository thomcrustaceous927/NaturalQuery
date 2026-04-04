# NaturalQuery

NL2SQL engine for .NET — convert natural language questions into SQL queries using LLMs.

```
"top 10 products by sales"  →  SELECT product AS label, SUM(amount) AS value FROM orders GROUP BY product ORDER BY value DESC LIMIT 10
```

## What it does

NaturalQuery takes a natural language question, sends it to an LLM with your database schema, and returns:
- A validated SQL query
- A recommended chart type (bar, pie, line, table, etc.)
- Title, description, and follow-up suggestions

It then executes the query and returns structured results ready for visualization.

## Quick Start

```bash
dotnet add package NaturalQuery
```

```csharp
builder.Services.AddNaturalQuery(options =>
{
    options.Tables = new List<TableSchema>
    {
        new("orders", new[]
        {
            new ColumnDef("id", "string"),
            new ColumnDef("product", "string"),
            new ColumnDef("amount", "double"),
            new ColumnDef("status", "string", "Active, Cancelled, Refunded"),
            new ColumnDef("created_at", "string", "ISO 8601 timestamp"),
        })
    };
    options.TenantIdColumn = "tenant_id";
    options.TenantIdPlaceholder = "{TENANT_ID}";
})
.UseBedrockProvider("us.anthropic.claude-haiku-4-5-20251001-v1:0")
.UseAthenaExecutor("my_database", "my_workgroup", "s3://my-results/");
```

```csharp
app.MapGet("/ask", async (string question, INaturalQueryEngine engine) =>
{
    var result = await engine.AskAsync(question, tenantId: "my-tenant");
    return Results.Ok(result);
});
```

## Architecture

```
Question (natural language)
    ↓
ILlmProvider (AWS Bedrock / custom)
    ↓
SQL generation + validation
    ↓
IQueryExecutor (Amazon Athena / custom)
    ↓
QueryResult (data + chart type + metadata)
```

## Built-in Providers

| Provider | Component | Package |
|----------|-----------|---------|
| AWS Bedrock (Claude) | `ILlmProvider` | Built-in |
| Amazon Athena | `IQueryExecutor` | Built-in |

### Custom Providers

Implement `ILlmProvider` for other LLMs (OpenAI, Ollama, etc.):

```csharp
public class OpenAiProvider : ILlmProvider
{
    public async Task<LlmResponse> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        // Your implementation
    }
}

builder.Services.AddNaturalQuery(options => { ... })
    .UseLlmProvider<OpenAiProvider>()
    .UseQueryExecutor<PostgresExecutor>();
```

Implement `IQueryExecutor` for other databases (PostgreSQL, BigQuery, etc.):

```csharp
public class PostgresExecutor : IQueryExecutor
{
    public async Task<List<DataPoint>> ExecuteChartQueryAsync(string sql, CancellationToken ct) { ... }
    public async Task<List<Dictionary<string, string>>> ExecuteTableQueryAsync(string sql, CancellationToken ct) { ... }
}
```

## Features

- **Schema-aware**: Auto-generates system prompts from your table definitions
- **Multi-tenant**: Built-in tenant isolation with placeholder replacement
- **SQL validation**: Blocks destructive queries (DELETE, DROP, etc.)
- **CTE support**: Handles `WITH ... AS (...)` queries for deduplication
- **Chart type inference**: LLM picks the best visualization for each question
- **Customizable**: Override the system prompt, add rules, block keywords

## Configuration

```csharp
options.Tables = new List<TableSchema> { ... };       // Your database schema
options.TenantIdColumn = "tenant_id";                  // Multi-tenant column
options.TenantIdPlaceholder = "{TENANT_ID}";           // Placeholder in SQL
options.MaxTokens = 1000;                              // LLM max tokens
options.Temperature = 0.1;                             // LLM temperature
options.CustomSystemPrompt = "...{TABLES_SCHEMA}...";  // Override prompt
options.AdditionalRules = new() { "Use UTC dates" };   // Extra instructions
options.ForbiddenSqlKeywords = new() { "UNION " };     // Extra blocked keywords
```

## License

MIT
