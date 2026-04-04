using NaturalQuery.Models;

namespace NaturalQuery.Providers;

/// <summary>
/// Abstraction for SQL query execution engines (Athena, BigQuery, PostgreSQL, etc.).
/// Implement this interface to add support for a new query backend.
/// </summary>
public interface IQueryExecutor
{
    /// <summary>
    /// Executes a SQL query and returns chart-friendly results (label + value pairs).
    /// Used for line, bar, pie, donut, area, and metric charts.
    /// </summary>
    Task<List<DataPoint>> ExecuteChartQueryAsync(string sql, CancellationToken ct = default);

    /// <summary>
    /// Executes a SQL query and returns raw tabular results.
    /// Used for table-type visualizations.
    /// </summary>
    Task<List<Dictionary<string, string>>> ExecuteTableQueryAsync(string sql, CancellationToken ct = default);
}
