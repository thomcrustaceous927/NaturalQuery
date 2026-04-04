using Amazon.Athena;
using Amazon.BedrockRuntime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NaturalQuery.Providers;

namespace NaturalQuery.Extensions;

/// <summary>
/// Extension methods for registering NaturalQuery services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers NaturalQuery services with the given configuration.
    /// Returns a builder for chaining provider configuration.
    /// </summary>
    public static NaturalQueryBuilder AddNaturalQuery(
        this IServiceCollection services,
        Action<NaturalQueryOptions> configure)
    {
        services.Configure(configure);
        services.AddScoped<INaturalQueryEngine, NaturalQueryEngine>();
        return new NaturalQueryBuilder(services);
    }
}

/// <summary>
/// Fluent builder for configuring NaturalQuery providers.
/// </summary>
public class NaturalQueryBuilder
{
    private readonly IServiceCollection _services;

    public NaturalQueryBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Use AWS Bedrock as the LLM provider.
    /// </summary>
    /// <param name="modelId">Bedrock model ID (e.g., "us.anthropic.claude-haiku-4-5-20251001-v1:0")</param>
    public NaturalQueryBuilder UseBedrockProvider(string modelId)
    {
        _services.AddSingleton<ILlmProvider>(sp =>
        {
            var client = sp.GetRequiredService<IAmazonBedrockRuntime>();
            var options = sp.GetRequiredService<IOptions<NaturalQueryOptions>>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BedrockProvider>>();
            return new BedrockProvider(client, modelId, options, logger);
        });
        return this;
    }

    /// <summary>
    /// Use a custom LLM provider.
    /// </summary>
    public NaturalQueryBuilder UseLlmProvider<T>() where T : class, ILlmProvider
    {
        _services.AddScoped<ILlmProvider, T>();
        return this;
    }

    /// <summary>
    /// Use Amazon Athena as the query executor.
    /// </summary>
    public NaturalQueryBuilder UseAthenaExecutor(string database, string workgroup, string outputLocation, int timeoutSeconds = 30)
    {
        _services.AddSingleton<IQueryExecutor>(sp =>
        {
            var client = sp.GetRequiredService<IAmazonAthena>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AthenaQueryExecutor>>();
            return new AthenaQueryExecutor(client, database, workgroup, outputLocation, logger, timeoutSeconds);
        });
        return this;
    }

    /// <summary>
    /// Use a custom query executor.
    /// </summary>
    public NaturalQueryBuilder UseQueryExecutor<T>() where T : class, IQueryExecutor
    {
        _services.AddScoped<IQueryExecutor, T>();
        return this;
    }
}
