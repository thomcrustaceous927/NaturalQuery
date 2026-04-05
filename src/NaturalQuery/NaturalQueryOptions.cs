using NaturalQuery.Models;

namespace NaturalQuery;

/// <summary>
/// Configuration options for the NaturalQuery engine.
/// </summary>
public class NaturalQueryOptions
{
    /// <summary>Database table schemas available for querying.</summary>
    public List<TableSchema> Tables { get; set; } = new();

    /// <summary>
    /// Placeholder string in generated SQL that will be replaced by the actual tenant ID.
    /// Example: "{TENANT_ID}"
    /// </summary>
    public string? TenantIdPlaceholder { get; set; }

    /// <summary>
    /// Column name used for tenant isolation (e.g., "tenant_id", "clientid").
    /// When set, all queries MUST include a WHERE filter on this column.
    /// </summary>
    public string? TenantIdColumn { get; set; }

    /// <summary>Maximum tokens for LLM response. Default: 1000.</summary>
    public int MaxTokens { get; set; } = 1000;

    /// <summary>LLM temperature (0.0 = deterministic, 1.0 = creative). Default: 0.1.</summary>
    public double Temperature { get; set; } = 0.1;

    /// <summary>
    /// Override the entire system prompt. When set, the auto-generated schema prompt is ignored.
    /// Use {TABLES_SCHEMA} placeholder to inject the auto-generated schema into a custom prompt.
    /// </summary>
    public string? CustomSystemPrompt { get; set; }

    /// <summary>Cache time-to-live in minutes. Default: 5. Set to 0 to disable caching.</summary>
    public int CacheTtlMinutes { get; set; } = 5;

    /// <summary>Maximum requests per minute per tenant for rate limiting. Default: 60.</summary>
    public int RateLimitPerMinute { get; set; } = 60;

    /// <summary>Additional SQL keywords to block (beyond the built-in list).</summary>
    public List<string> ForbiddenSqlKeywords { get; set; } = new();

    /// <summary>
    /// Additional rules/instructions to append to the system prompt.
    /// Useful for domain-specific guidance without replacing the entire prompt.
    /// </summary>
    public List<string> AdditionalRules { get; set; } = new();

    /// <summary>
    /// Maximum number of retry attempts when a generated query fails to execute.
    /// The engine sends the error back to the LLM and asks it to fix the SQL.
    /// Default: 0 (no retries). Maximum: 3.
    /// </summary>
    public int MaxRetries { get; set; } = 0;
}
