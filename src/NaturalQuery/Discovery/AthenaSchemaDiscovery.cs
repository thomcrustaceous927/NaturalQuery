using Amazon.Athena;
using Amazon.Athena.Model;
using Microsoft.Extensions.Logging;
using NaturalQuery.Models;

namespace NaturalQuery.Discovery;

/// <summary>
/// Discovers table schemas from an Amazon Athena/Glue catalog.
/// </summary>
public class AthenaSchemaDiscovery : ISchemaDiscovery
{
    private readonly IAmazonAthena _athenaClient;
    private readonly string _database;
    private readonly ILogger<AthenaSchemaDiscovery> _logger;

    /// <summary>
    /// Initializes Athena schema discovery.
    /// </summary>
    /// <param name="athenaClient">Amazon Athena client.</param>
    /// <param name="database">Glue catalog database name.</param>
    /// <param name="logger">Logger instance.</param>
    public AthenaSchemaDiscovery(IAmazonAthena athenaClient, string database, ILogger<AthenaSchemaDiscovery> logger)
    {
        _athenaClient = athenaClient;
        _database = database;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<TableSchema>> DiscoverAsync(string? schemaFilter = null, CancellationToken ct = default)
    {
        _logger.LogInformation("[SchemaDiscovery] Discovering tables in Athena database '{Database}'", _database);

        var tablesResponse = await _athenaClient.ListTableMetadataAsync(new ListTableMetadataRequest
        {
            CatalogName = "AwsDataCatalog",
            DatabaseName = _database
        }, ct);

        var result = new List<TableSchema>();

        foreach (var tableMeta in tablesResponse.TableMetadataList)
        {
            var table = new TableSchema
            {
                Name = tableMeta.Name,
                Description = tableMeta.TableType
            };

            foreach (var col in tableMeta.Columns)
            {
                table.Columns.Add(new ColumnDef(col.Name, MapAthenaType(col.Type), col.Comment));
            }

            foreach (var part in tableMeta.PartitionKeys)
            {
                table.Partitions.Add(part.Name);
                // Also add partition columns to the column list
                table.Columns.Add(new ColumnDef(part.Name, MapAthenaType(part.Type), $"partition: {part.Comment}"));
            }

            result.Add(table);
        }

        _logger.LogInformation("[SchemaDiscovery] Discovered {Count} tables from Athena", result.Count);

        return result;
    }

    private static string MapAthenaType(string athenaType) => athenaType.ToLowerInvariant() switch
    {
        "int" or "integer" or "bigint" or "smallint" or "tinyint" => "int",
        "double" or "float" or "decimal" => "double",
        "boolean" => "boolean",
        "date" => "date",
        "timestamp" => "timestamp",
        _ => "string"
    };
}
