using NaturalQuery.Models;

namespace NaturalQuery;

/// <summary>
/// Main entry point for NL2SQL — converts natural language questions to SQL queries,
/// executes them, and returns structured results.
/// </summary>
public interface INaturalQueryEngine
{
    /// <summary>
    /// Interprets a natural language question, generates SQL, executes it,
    /// and returns the results with chart metadata.
    /// </summary>
    /// <param name="question">Natural language question (e.g., "top 10 products by sales")</param>
    /// <param name="tenantId">Optional tenant ID for multi-tenant filtering</param>
    /// <param name="ct">Cancellation token</param>
    Task<QueryResult> AskAsync(string question, string? tenantId = null, CancellationToken ct = default);

    /// <summary>
    /// Interprets a natural language question and returns the generated SQL
    /// without executing it. Useful for preview/validation.
    /// </summary>
    Task<QueryResult> InterpretAsync(string question, string? tenantId = null, CancellationToken ct = default);
}
