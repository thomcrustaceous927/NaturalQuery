using NaturalQuery.Models;

namespace NaturalQuery;

/// <summary>
/// Main entry point for NL2SQL — converts natural language questions to SQL queries,
/// executes them, and returns structured results with visualization metadata.
/// </summary>
public interface INaturalQueryEngine
{
    /// <summary>
    /// Interprets a natural language question, generates SQL, executes it,
    /// and returns the results with chart metadata. Supports caching, rate limiting,
    /// and conversation context for follow-up questions.
    /// </summary>
    /// <param name="question">Natural language question (e.g., "top 10 products by sales").</param>
    /// <param name="tenantId">Optional tenant ID for multi-tenant filtering.</param>
    /// <param name="context">Optional conversation context for follow-up questions.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<QueryResult> AskAsync(string question, string? tenantId = null, ConversationContext? context = null, CancellationToken ct = default);

    /// <summary>
    /// Interprets a natural language question and returns the generated SQL
    /// without executing it. Useful for preview, validation, or cost estimation.
    /// </summary>
    /// <param name="question">Natural language question.</param>
    /// <param name="tenantId">Optional tenant ID for multi-tenant filtering.</param>
    /// <param name="context">Optional conversation context for follow-up questions.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<QueryResult> InterpretAsync(string question, string? tenantId = null, ConversationContext? context = null, CancellationToken ct = default);

    /// <summary>
    /// Uses the LLM to explain a SQL query in plain language.
    /// Useful for transparency — lets users understand what the generated query does.
    /// </summary>
    /// <param name="sql">The SQL query to explain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Human-readable explanation of the query.</returns>
    Task<string> ExplainAsync(string sql, CancellationToken ct = default);

    /// <summary>
    /// Uses the LLM to suggest useful questions based on the configured table schemas.
    /// Helps users discover what they can ask.
    /// </summary>
    /// <param name="count">Number of suggestions to generate. Default: 5.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of suggested natural language questions.</returns>
    Task<List<string>> SuggestQuestionsAsync(int count = 5, CancellationToken ct = default);
}
