using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NaturalQuery.Models;
using NaturalQuery.Providers;
using NaturalQuery.Validation;

namespace NaturalQuery;

/// <summary>
/// Core NL2SQL engine. Converts natural language questions into SQL queries,
/// validates them, executes them, and returns structured results.
/// </summary>
public class NaturalQueryEngine : INaturalQueryEngine
{
    private readonly ILlmProvider _llmProvider;
    private readonly IQueryExecutor _queryExecutor;
    private readonly NaturalQueryOptions _options;
    private readonly ILogger<NaturalQueryEngine> _logger;

    public NaturalQueryEngine(
        ILlmProvider llmProvider,
        IQueryExecutor queryExecutor,
        IOptions<NaturalQueryOptions> options,
        ILogger<NaturalQueryEngine> logger)
    {
        _llmProvider = llmProvider;
        _queryExecutor = queryExecutor;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<QueryResult> AskAsync(string question, string? tenantId = null, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var result = await InterpretAsync(question, tenantId, ct);

        // Replace tenant placeholder
        var sql = result.Sql;
        if (!string.IsNullOrEmpty(_options.TenantIdPlaceholder) && !string.IsNullOrEmpty(tenantId))
            sql = sql.Replace(_options.TenantIdPlaceholder, tenantId);

        // Validate SQL
        var error = SqlValidator.Validate(sql, _options.TenantIdColumn, tenantId, _options.ForbiddenSqlKeywords);
        if (error != null)
            throw new InvalidOperationException($"Invalid query: {error}");

        // Execute
        if (result.ChartType == ChartType.Table)
        {
            result.TableData = await _queryExecutor.ExecuteTableQueryAsync(sql, ct);
        }
        else
        {
            result.ChartData = await _queryExecutor.ExecuteChartQueryAsync(sql, ct);
        }

        stopwatch.Stop();
        result.ElapsedMs = stopwatch.ElapsedMilliseconds;

        _logger.LogInformation("NaturalQuery completed in {ElapsedMs}ms. Chart: {ChartType}, Tokens: {Tokens}",
            result.ElapsedMs, result.ChartType, result.TokensUsed);

        return result;
    }

    /// <inheritdoc />
    public async Task<QueryResult> InterpretAsync(string question, string? tenantId = null, CancellationToken ct = default)
    {
        var systemPrompt = BuildSystemPrompt();
        var response = await _llmProvider.GenerateAsync(systemPrompt, question, ct);

        return ParseResponse(response);
    }

    public string BuildSystemPrompt()
    {
        if (!string.IsNullOrEmpty(_options.CustomSystemPrompt))
        {
            var schemaText = BuildSchemaText();
            return _options.CustomSystemPrompt.Replace("{TABLES_SCHEMA}", schemaText);
        }

        return GenerateDefaultPrompt();
    }

    private string GenerateDefaultPrompt()
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are a SQL report generator. Interpret natural language questions and return a JSON object with a valid SQL query.");
        sb.AppendLine();
        sb.AppendLine("=== AVAILABLE TABLES ===");
        sb.AppendLine();
        sb.Append(BuildSchemaText());
        sb.AppendLine();

        sb.AppendLine("=== CHART TYPES ===");
        sb.AppendLine("- \"line\": time series (SELECT period AS label, COUNT(*) AS value)");
        sb.AppendLine("- \"bar\": category comparisons (SELECT field AS label, COUNT(*) AS value)");
        sb.AppendLine("- \"bar_horizontal\": rankings (SELECT field AS label, COUNT(*) AS value ORDER BY value DESC)");
        sb.AppendLine("- \"pie\" / \"donut\": distributions (SELECT field AS label, COUNT(*) AS value)");
        sb.AppendLine("- \"metric\": single number (SELECT COUNT(*) AS value)");
        sb.AppendLine("- \"area\": filled time series");
        sb.AppendLine("- \"table\": detailed data list (SELECT col1, col2, ... — no label/value)");
        sb.AppendLine();

        sb.AppendLine("=== SQL RULES ===");

        if (!string.IsNullOrEmpty(_options.TenantIdColumn) && !string.IsNullOrEmpty(_options.TenantIdPlaceholder))
        {
            sb.AppendLine($"1. ALWAYS include WHERE {_options.TenantIdColumn} = '{_options.TenantIdPlaceholder}' for tenant isolation.");
        }

        sb.AppendLine("2. DEDUPLICATION: If multiple records exist per entity (INSERT, MODIFY events), use ROW_NUMBER() OVER (PARTITION BY id ORDER BY _eventtime DESC) to get the latest state. Exclude _eventname = 'REMOVE'.");
        sb.AppendLine("3. For charts: return EXACTLY 2 columns: \"label\" and \"value\".");
        sb.AppendLine("4. For tables: use descriptive column aliases via AS.");
        sb.AppendLine("5. Use LIMIT to avoid returning too many rows (default: 100 for tables, 50 for rankings).");
        sb.AppendLine("6. NEVER use DELETE, UPDATE, INSERT, DROP, ALTER, CREATE. Only SELECT.");
        sb.AppendLine();

        if (_options.AdditionalRules.Count > 0)
        {
            sb.AppendLine("=== ADDITIONAL RULES ===");
            foreach (var rule in _options.AdditionalRules)
                sb.AppendLine($"- {rule}");
            sb.AppendLine();
        }

        sb.AppendLine("=== RESPONSE FORMAT ===");
        sb.AppendLine("{\"sql\":\"SELECT ...\",\"chartType\":\"table\",\"title\":\"...\",\"description\":\"...\",\"suggestions\":[\"...\"]}");
        sb.AppendLine();

        sb.AppendLine("=== GUARDRAILS ===");
        sb.AppendLine("1. Return ONLY pure JSON. No markdown, no extra text.");
        sb.AppendLine("2. The \"sql\" field is REQUIRED in every response.");
        sb.AppendLine("3. Title and description should be concise.");
        sb.AppendLine("4. Suggestions: up to 3 follow-up report ideas.");

        return sb.ToString();
    }

    private string BuildSchemaText()
    {
        var sb = new StringBuilder();
        foreach (var table in _options.Tables)
        {
            var desc = string.IsNullOrEmpty(table.Description) ? "" : $" — {table.Description}";
            var partitions = table.Partitions.Count > 0
                ? $" (partitions: {string.Join(", ", table.Partitions)})"
                : "";

            sb.AppendLine($"{table.Name}{partitions}{desc}");
            sb.Append("  Columns: ");
            sb.AppendJoin(", ", table.Columns.Select(c =>
            {
                var colDesc = string.IsNullOrEmpty(c.Description) ? "" : $" ({c.Description})";
                return $"{c.Name} ({c.Type}){colDesc}";
            }));
            sb.AppendLine();
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public static QueryResult ParseResponse(LlmResponse response)
    {
        var text = response.Text.Trim();

        // Strip markdown fences
        if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline > 0) text = text[(firstNewline + 1)..];
            if (text.EndsWith("```")) text = text[..^3];
            text = text.Trim();
        }

        // Extract JSON object
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end < 0 || end <= start)
            throw new InvalidOperationException("Could not parse LLM response as JSON.");

        text = text[start..(end + 1)];

        JsonElement root;
        try
        {
            root = JsonSerializer.Deserialize<JsonElement>(text);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Could not parse LLM response as JSON.");
        }

        // Check for error response
        if (root.TryGetProperty("error", out var errProp) && errProp.GetBoolean())
        {
            var msg = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Unknown error";
            throw new InvalidOperationException(msg);
        }

        // Extract SQL (required)
        var sql = root.TryGetProperty("sql", out var sqlProp) ? sqlProp.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(sql))
            throw new InvalidOperationException("LLM response is missing the 'sql' field.");

        // Validate SQL
        var sqlUpper = sql.Trim().ToUpperInvariant();
        if (!sqlUpper.StartsWith("SELECT") && !sqlUpper.StartsWith("WITH"))
            throw new InvalidOperationException("Only SELECT queries are allowed.");

        // Remove string literals for false-positive prevention
        var sqlNoStrings = Regex.Replace(sqlUpper, "'[^']*'", "''");
        var forbidden = new[] { "DELETE ", "UPDATE ", " INSERT INTO", "DROP ", "ALTER ", "CREATE ", "TRUNCATE ", "GRANT ", "REVOKE " };
        foreach (var word in forbidden)
        {
            if (sqlNoStrings.Contains(word))
                throw new InvalidOperationException($"Forbidden SQL keyword: {word.Trim()}");
        }

        // Chart type
        var chartType = root.TryGetProperty("chartType", out var ctProp) ? ctProp.GetString() ?? "table" : "table";
        if (!ChartType.IsValid(chartType)) chartType = "table";

        // Title & description
        var title = root.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";
        var description = root.TryGetProperty("description", out var descProp) ? descProp.GetString() ?? "" : "";

        // Suggestions
        var suggestions = new List<string>();
        if (root.TryGetProperty("suggestions", out var sugProp) && sugProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in sugProp.EnumerateArray())
            {
                var s = item.GetString();
                if (!string.IsNullOrWhiteSpace(s) && suggestions.Count < 3)
                    suggestions.Add(s);
            }
        }

        return new QueryResult
        {
            Sql = sql,
            ChartType = chartType,
            Title = title,
            Description = description,
            Suggestions = suggestions,
            TokensUsed = response.TokensUsed
        };
    }
}
