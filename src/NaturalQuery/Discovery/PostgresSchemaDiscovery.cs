using Microsoft.Extensions.Logging;
using Npgsql;
using NaturalQuery.Models;

namespace NaturalQuery.Discovery;

/// <summary>
/// Discovers table schemas from a PostgreSQL database using information_schema.
/// </summary>
public class PostgresSchemaDiscovery : ISchemaDiscovery
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresSchemaDiscovery> _logger;

    /// <summary>
    /// Initializes PostgreSQL schema discovery.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="logger">Logger instance.</param>
    public PostgresSchemaDiscovery(string connectionString, ILogger<PostgresSchemaDiscovery> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<TableSchema>> DiscoverAsync(string? schemaFilter = null, CancellationToken ct = default)
    {
        var schema = schemaFilter ?? "public";

        _logger.LogInformation("[SchemaDiscovery] Discovering tables in schema '{Schema}'", schema);

        var tables = new Dictionary<string, TableSchema>();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = @"
            SELECT table_name, column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE table_schema = @schema
            ORDER BY table_name, ordinal_position";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("schema", schema);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var tableName = reader.GetString(0);
            var columnName = reader.GetString(1);
            var dataType = reader.GetString(2);
            var isNullable = reader.GetString(3);

            if (!tables.ContainsKey(tableName))
                tables[tableName] = new TableSchema { Name = tableName };

            var mappedType = MapPostgresType(dataType);
            var desc = isNullable == "YES" ? "nullable" : null;

            tables[tableName].Columns.Add(new ColumnDef(columnName, mappedType, desc));
        }

        _logger.LogInformation("[SchemaDiscovery] Discovered {Count} tables", tables.Count);

        return tables.Values.ToList();
    }

    private static string MapPostgresType(string pgType) => pgType.ToLowerInvariant() switch
    {
        "integer" or "bigint" or "smallint" or "serial" or "bigserial" => "int",
        "numeric" or "decimal" or "real" or "double precision" or "money" => "double",
        "boolean" => "boolean",
        "date" => "date",
        "timestamp without time zone" or "timestamp with time zone" => "timestamp",
        "json" or "jsonb" => "json",
        "uuid" => "string",
        _ => "string"
    };
}
