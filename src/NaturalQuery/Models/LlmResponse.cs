namespace NaturalQuery.Models;

/// <summary>
/// Raw response from the LLM provider.
/// </summary>
public record LlmResponse(string Text, int TokensUsed);
