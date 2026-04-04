namespace NaturalQuery.Models;

/// <summary>
/// Estimated cost of executing a SQL query.
/// </summary>
public record QueryCostEstimate(
    /// <summary>Estimated bytes to be scanned.</summary>
    long EstimatedBytes,
    /// <summary>Estimated cost in USD (based on $5/TB for Athena).</summary>
    decimal EstimatedCostUsd
)
{
    /// <summary>Estimated bytes in human-readable format (KB, MB, GB).</summary>
    public string FormattedSize => EstimatedBytes switch
    {
        < 1024 => $"{EstimatedBytes} B",
        < 1024 * 1024 => $"{EstimatedBytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{EstimatedBytes / (1024.0 * 1024.0):F1} MB",
        _ => $"{EstimatedBytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
    };
}
