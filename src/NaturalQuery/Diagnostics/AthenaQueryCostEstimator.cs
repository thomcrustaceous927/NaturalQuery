using Amazon.Athena;
using Amazon.Athena.Model;
using Microsoft.Extensions.Logging;
using NaturalQuery.Models;

namespace NaturalQuery.Diagnostics;

/// <summary>
/// Estimates query cost for Amazon Athena using EXPLAIN.
/// Athena charges $5 per TB scanned.
/// </summary>
public class AthenaQueryCostEstimator : IQueryCostEstimator
{
    private readonly IAmazonAthena _athenaClient;
    private readonly string _database;
    private readonly string _workgroup;
    private readonly string _outputLocation;
    private readonly ILogger<AthenaQueryCostEstimator> _logger;

    /// <summary>Athena pricing: $5.00 per TB scanned.</summary>
    private const decimal PricePerTbUsd = 5.00m;

    /// <summary>
    /// Initializes the Athena cost estimator.
    /// </summary>
    /// <param name="athenaClient">Amazon Athena client.</param>
    /// <param name="database">Glue catalog database name.</param>
    /// <param name="workgroup">Athena workgroup name.</param>
    /// <param name="outputLocation">S3 output location for query results.</param>
    /// <param name="logger">Logger instance.</param>
    public AthenaQueryCostEstimator(
        IAmazonAthena athenaClient,
        string database,
        string workgroup,
        string outputLocation,
        ILogger<AthenaQueryCostEstimator> logger)
    {
        _athenaClient = athenaClient;
        _database = database;
        _workgroup = workgroup;
        _outputLocation = outputLocation;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<QueryCostEstimate> EstimateAsync(string sql, CancellationToken ct = default)
    {
        _logger.LogInformation("[CostEstimator] Estimating cost for query");

        var startResponse = await _athenaClient.StartQueryExecutionAsync(new StartQueryExecutionRequest
        {
            QueryString = $"EXPLAIN {sql}",
            QueryExecutionContext = new QueryExecutionContext { Database = _database },
            WorkGroup = _workgroup,
            ResultConfiguration = new ResultConfiguration { OutputLocation = _outputLocation }
        }, ct);

        // Wait for EXPLAIN to complete
        var elapsed = 0;
        while (elapsed < 15000)
        {
            await Task.Delay(1000, ct);
            elapsed += 1000;

            var statusResponse = await _athenaClient.GetQueryExecutionAsync(new GetQueryExecutionRequest
            {
                QueryExecutionId = startResponse.QueryExecutionId
            }, ct);

            var state = statusResponse.QueryExecution.Status.State;

            if (state == QueryExecutionState.SUCCEEDED)
            {
                var bytesScanned = statusResponse.QueryExecution.Statistics?.DataScannedInBytes ?? 0;

                // EXPLAIN itself scans minimal data; estimate actual scan from query structure
                // Use a heuristic: EXPLAIN scans ~1% of actual data
                var estimatedBytes = bytesScanned * 100;
                var estimatedCost = (decimal)estimatedBytes / (1024m * 1024m * 1024m * 1024m) * PricePerTbUsd;

                _logger.LogInformation("[CostEstimator] Estimated: {Bytes} bytes, ${Cost:F6} USD",
                    estimatedBytes, estimatedCost);

                return new QueryCostEstimate(estimatedBytes, Math.Round(estimatedCost, 6));
            }

            if (state == QueryExecutionState.FAILED || state == QueryExecutionState.CANCELLED)
            {
                _logger.LogWarning("[CostEstimator] EXPLAIN query failed, returning zero estimate");
                return new QueryCostEstimate(0, 0);
            }
        }

        return new QueryCostEstimate(0, 0);
    }
}
