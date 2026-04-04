using Microsoft.Extensions.Logging;
using Npgsql;
using NaturalQuery.Models;

namespace NaturalQuery.Providers;

/// <summary>
/// Query executor for PostgreSQL databases using Npgsql.
/// </summary>
public class PostgresQueryExecutor : IQueryExecutor
{
    private readonly string _connectionString;
    private readonly int _timeoutSeconds;
    private readonly ILogger<PostgresQueryExecutor> _logger;

    /// <summary>
    /// Initializes the PostgreSQL query executor.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="timeoutSeconds">Command timeout in seconds. Default: 30.</param>
    public PostgresQueryExecutor(
        string connectionString,
        ILogger<PostgresQueryExecutor> logger,
        int timeoutSeconds = 30)
    {
        _connectionString = connectionString;
        _logger = logger;
        _timeoutSeconds = timeoutSeconds;
    }

    /// <inheritdoc />
    public async Task<List<DataPoint>> ExecuteChartQueryAsync(string sql, CancellationToken ct = default)
    {
        _logger.LogInformation("[Postgres] Executing chart query: {Sql}", sql[..Math.Min(200, sql.Length)]);

        var results = new List<DataPoint>();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = _timeoutSeconds };
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var label = reader.GetValue(0)?.ToString() ?? "";
            var rawValue = reader.GetValue(reader.FieldCount - 1);

            if (rawValue != null && double.TryParse(rawValue.ToString(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                results.Add(new DataPoint(label, value));
            }
        }

        _logger.LogInformation("[Postgres] Chart query returned {Count} data points", results.Count);
        return results;
    }

    /// <inheritdoc />
    public async Task<List<Dictionary<string, string>>> ExecuteTableQueryAsync(string sql, CancellationToken ct = default)
    {
        _logger.LogInformation("[Postgres] Executing table query: {Sql}", sql[..Math.Min(200, sql.Length)]);

        var results = new List<Dictionary<string, string>>();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = _timeoutSeconds };
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, string>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.GetValue(i)?.ToString() ?? "";
            }
            results.Add(row);
        }

        _logger.LogInformation("[Postgres] Table query returned {Count} rows", results.Count);
        return results;
    }
}
