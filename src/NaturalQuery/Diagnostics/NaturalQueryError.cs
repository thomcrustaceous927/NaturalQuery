namespace NaturalQuery.Diagnostics;

/// <summary>
/// Represents an error that occurred during NL2SQL processing.
/// Used by the error handler callback for logging/tracking.
/// </summary>
public class NaturalQueryError
{
    /// <summary>The original natural language question.</summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>The generated SQL query (if available before the error).</summary>
    public string? Sql { get; set; }

    /// <summary>Error classification: "llm", "validation", "execution", "timeout", "rate_limit", "parsing".</summary>
    public string ErrorType { get; set; } = string.Empty;

    /// <summary>Error message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Tenant ID (if multi-tenant).</summary>
    public string? TenantId { get; set; }

    /// <summary>Tokens consumed by the LLM (if available).</summary>
    public int? TokensUsed { get; set; }

    /// <summary>Elapsed time in milliseconds when the error occurred.</summary>
    public long ElapsedMs { get; set; }

    /// <summary>Timestamp of the error.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>The inner exception (if available).</summary>
    public Exception? Exception { get; set; }
}
