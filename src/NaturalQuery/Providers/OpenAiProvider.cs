using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NaturalQuery.Models;

namespace NaturalQuery.Providers;

/// <summary>
/// LLM provider for OpenAI-compatible APIs (OpenAI, Azure OpenAI, Ollama, etc.).
/// Uses HttpClient directly — no SDK dependency required.
/// </summary>
public class OpenAiProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly string _apiKey;
    private readonly int _maxTokens;
    private readonly double _temperature;
    private readonly ILogger<OpenAiProvider> _logger;

    /// <summary>
    /// Initializes the OpenAI provider.
    /// </summary>
    /// <param name="httpClient">HttpClient (can be pre-configured with base URL for custom endpoints).</param>
    /// <param name="apiKey">API key for authentication.</param>
    /// <param name="model">Model name (e.g., "gpt-4o-mini", "gpt-4o"). Default: "gpt-4o-mini".</param>
    /// <param name="maxTokens">Maximum tokens for the response. Default: 1000.</param>
    /// <param name="temperature">Temperature for generation (0.0-2.0). Default: 0.1.</param>
    /// <param name="logger">Logger instance.</param>
    public OpenAiProvider(
        HttpClient httpClient,
        string apiKey,
        string model = "gpt-4o-mini",
        int maxTokens = 1000,
        double temperature = 0.1,
        ILogger<OpenAiProvider>? logger = null)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;
        _maxTokens = maxTokens;
        _temperature = temperature;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<OpenAiProvider>.Instance;

        // Set default base address if not configured
        if (_httpClient.BaseAddress == null)
            _httpClient.BaseAddress = new Uri("https://api.openai.com/");
    }

    /// <inheritdoc />
    public async Task<LlmResponse> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var request = new
        {
            model = _model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            max_tokens = _maxTokens,
            temperature = _temperature
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions");
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        httpRequest.Content = JsonContent.Create(request);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[OpenAI] Request failed");
            throw new InvalidOperationException("Failed to connect to OpenAI API.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("[OpenAI] Error {StatusCode}: {Body}", response.StatusCode, errorBody);

            if ((int)response.StatusCode == 429)
                throw new InvalidOperationException("OpenAI rate limit reached. Try again later.");

            throw new InvalidOperationException($"OpenAI API error ({response.StatusCode}): {errorBody}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var text = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        var promptTokens = root.GetProperty("usage").GetProperty("prompt_tokens").GetInt32();
        var completionTokens = root.GetProperty("usage").GetProperty("completion_tokens").GetInt32();
        var totalTokens = promptTokens + completionTokens;

        _logger.LogInformation("[OpenAI] Response received. Model: {Model}, Tokens: {Tokens}", _model, totalTokens);

        return new LlmResponse(text, totalTokens);
    }
}
