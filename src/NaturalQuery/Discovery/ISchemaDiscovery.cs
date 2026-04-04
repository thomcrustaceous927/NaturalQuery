using NaturalQuery.Models;

namespace NaturalQuery.Discovery;

/// <summary>
/// Interface for automatic schema discovery from a database.
/// Connects to the database and infers table schemas.
/// </summary>
public interface ISchemaDiscovery
{
    /// <summary>
    /// Discovers all tables and their columns from the database.
    /// </summary>
    /// <param name="schemaFilter">Optional schema/namespace filter (e.g., "public" for Postgres).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of discovered table schemas.</returns>
    Task<List<TableSchema>> DiscoverAsync(string? schemaFilter = null, CancellationToken ct = default);
}
