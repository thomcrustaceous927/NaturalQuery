using NaturalQuery.Models;

namespace NaturalQuery.Diagnostics;

/// <summary>
/// Interface for estimating the cost of a SQL query before execution.
/// </summary>
public interface IQueryCostEstimator
{
    /// <summary>
    /// Estimates the cost of executing a SQL query.
    /// </summary>
    /// <param name="sql">The SQL query to estimate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Cost estimate with bytes and USD.</returns>
    Task<QueryCostEstimate> EstimateAsync(string sql, CancellationToken ct = default);
}
