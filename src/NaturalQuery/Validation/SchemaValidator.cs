using System.Text.RegularExpressions;
using NaturalQuery.Models;

namespace NaturalQuery.Validation;

/// <summary>
/// Validates SQL queries against the defined table schemas.
/// Checks that referenced tables and columns exist in the schema.
/// </summary>
public static class SchemaValidator
{
    /// <summary>
    /// Validates that column references in the SQL exist in the provided schemas.
    /// Returns null if valid, or an error message listing unknown columns.
    /// </summary>
    /// <param name="sql">The SQL query to validate.</param>
    /// <param name="tables">Available table schemas.</param>
    /// <returns>Null if valid, or error message if unknown columns are found.</returns>
    public static string? ValidateColumns(string sql, IEnumerable<TableSchema> tables)
    {
        if (string.IsNullOrWhiteSpace(sql) || tables == null)
            return null;

        var allColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in tables)
        {
            tableNames.Add(table.Name);
            foreach (var col in table.Columns)
                allColumns.Add(col.Name);
        }

        // Extract table references from FROM and JOIN clauses
        var tablePattern = new Regex(@"(?:FROM|JOIN)\s+(\w+)", RegexOptions.IgnoreCase);
        var referencedTables = tablePattern.Matches(sql)
            .Select(m => m.Groups[1].Value)
            .Where(t => !t.Equals("SELECT", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Check for unknown tables (skip CTE names and subquery aliases)
        var unknownTables = referencedTables
            .Where(t => !tableNames.Contains(t) && !IsCteAlias(sql, t))
            .ToList();

        if (unknownTables.Count > 0)
            return $"Unknown table(s): {string.Join(", ", unknownTables)}. Available: {string.Join(", ", tableNames)}";

        return null; // Valid
    }

    private static bool IsCteAlias(string sql, string name)
    {
        // Check if the name appears as a CTE alias: WITH name AS (...)
        var pattern = new Regex($@"\b{Regex.Escape(name)}\s+AS\s*\(", RegexOptions.IgnoreCase);
        return pattern.IsMatch(sql);
    }
}
