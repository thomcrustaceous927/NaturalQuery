using Microsoft.Extensions.Logging;

namespace NaturalQuery.Validation;

/// <summary>
/// Validates database connection strings for read-only safety.
/// Logs warnings when connections might allow write operations.
/// This is advisory only and does not block execution.
/// </summary>
public static class ConnectionValidator
{
    /// <summary>
    /// Checks if a SQL Server connection string is configured for read-only access.
    /// Looks for ApplicationIntent=ReadOnly in the connection string.
    /// </summary>
    public static bool IsSqlServerReadOnly(string connectionString)
    {
        return connectionString.Contains("ApplicationIntent=ReadOnly", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("ReadOnly=true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a PostgreSQL connection string suggests read-only mode.
    /// PostgreSQL doesn't have a direct connection string flag, so this checks
    /// for common patterns like read replicas.
    /// </summary>
    public static bool IsPostgresReadOnly(string connectionString)
    {
        // PostgreSQL read-only is typically set at the session level, not connection string.
        // We check for common patterns that suggest read-only intent.
        return connectionString.Contains("Target Session Attrs=read-only", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("Options=-c default_transaction_read_only=on", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Logs a warning if the connection string is not configured for read-only access.
    /// This is a best-practice advisory, not a blocker.
    /// </summary>
    public static void WarnIfNotReadOnly(string connectionString, string provider, ILogger logger)
    {
        var isReadOnly = provider.ToLowerInvariant() switch
        {
            "sqlserver" => IsSqlServerReadOnly(connectionString),
            "postgres" or "postgresql" => IsPostgresReadOnly(connectionString),
            "sqlite" => true, // SQLite with read-only can be set via Mode=ReadOnly in connection string
            "athena" => true, // Athena is always read-only
            _ => true // Unknown providers: don't warn
        };

        if (!isReadOnly)
        {
            logger.LogWarning(
                "[NaturalQuery] The {Provider} connection string is not configured for read-only access. " +
                "Consider using a read-only connection to prevent accidental writes. " +
                "For SQL Server, add 'ApplicationIntent=ReadOnly' to your connection string.",
                provider);
        }
    }
}
