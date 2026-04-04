using System.Diagnostics;

namespace NaturalQuery.Diagnostics;

/// <summary>
/// OpenTelemetry-ready diagnostics for NaturalQuery operations.
/// Creates Activities that can be collected by any OpenTelemetry-compatible exporter.
/// </summary>
public static class NaturalQueryDiagnostics
{
    /// <summary>ActivitySource name for tracing.</summary>
    public const string SourceName = "NaturalQuery";

    /// <summary>ActivitySource for creating spans.</summary>
    public static readonly ActivitySource ActivitySource = new(SourceName, "1.0.0");

    /// <summary>Starts a new activity for an Ask operation.</summary>
    public static Activity? StartAsk(string question, string? tenantId)
    {
        var activity = ActivitySource.StartActivity("NaturalQuery.Ask", ActivityKind.Client);
        activity?.SetTag("nq.question", question);
        activity?.SetTag("nq.tenant_id", tenantId ?? "none");
        return activity;
    }

    /// <summary>Starts a new activity for LLM generation.</summary>
    public static Activity? StartLlmGeneration(string modelInfo)
    {
        var activity = ActivitySource.StartActivity("NaturalQuery.LlmGenerate", ActivityKind.Client);
        activity?.SetTag("nq.model", modelInfo);
        return activity;
    }

    /// <summary>Starts a new activity for query execution.</summary>
    public static Activity? StartQueryExecution(string sql)
    {
        var activity = ActivitySource.StartActivity("NaturalQuery.Execute", ActivityKind.Client);
        activity?.SetTag("nq.sql_length", sql.Length);
        return activity;
    }

    /// <summary>Records result metadata on the current activity.</summary>
    public static void RecordResult(Activity? activity, int tokensUsed, string chartType, long elapsedMs)
    {
        activity?.SetTag("nq.tokens_used", tokensUsed);
        activity?.SetTag("nq.chart_type", chartType);
        activity?.SetTag("nq.elapsed_ms", elapsedMs);
    }

    /// <summary>Records a cache hit on the current activity.</summary>
    public static void RecordCacheHit(Activity? activity)
    {
        activity?.SetTag("nq.cache_hit", true);
    }

    /// <summary>Records an error on the current activity.</summary>
    public static void RecordError(Activity? activity, Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
        {
            { "exception.type", ex.GetType().FullName },
            { "exception.message", ex.Message }
        }));
    }
}
