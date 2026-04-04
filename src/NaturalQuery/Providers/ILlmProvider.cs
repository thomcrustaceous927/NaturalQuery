using NaturalQuery.Models;

namespace NaturalQuery.Providers;

/// <summary>
/// Abstraction for LLM providers (Bedrock, OpenAI, etc.).
/// Implement this interface to add support for a new LLM backend.
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Sends a system prompt and user prompt to the LLM and returns the response.
    /// </summary>
    Task<LlmResponse> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}
