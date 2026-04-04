using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NaturalQuery.Models;

namespace NaturalQuery.Providers;

/// <summary>
/// LLM provider using AWS Bedrock (Claude models).
/// </summary>
public class BedrockProvider : ILlmProvider
{
    private readonly IAmazonBedrockRuntime _bedrockClient;
    private readonly string _modelId;
    private readonly NaturalQueryOptions _options;
    private readonly ILogger<BedrockProvider> _logger;

    public BedrockProvider(
        IAmazonBedrockRuntime bedrockClient,
        string modelId,
        IOptions<NaturalQueryOptions> options,
        ILogger<BedrockProvider> logger)
    {
        _bedrockClient = bedrockClient;
        _modelId = modelId;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<LlmResponse> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var request = new ConverseRequest
        {
            ModelId = _modelId,
            System = new List<SystemContentBlock>
            {
                new() { Text = systemPrompt }
            },
            Messages = new List<Message>
            {
                new()
                {
                    Role = ConversationRole.User,
                    Content = new List<ContentBlock>
                    {
                        new() { Text = userPrompt }
                    }
                }
            },
            InferenceConfig = new InferenceConfiguration
            {
                MaxTokens = _options.MaxTokens,
                Temperature = (float)_options.Temperature
            }
        };

        try
        {
            var response = await _bedrockClient.ConverseAsync(request, ct);

            var text = response.Output.Message.Content[0].Text;
            var tokens = (response.Usage?.InputTokens ?? 0) + (response.Usage?.OutputTokens ?? 0);

            _logger.LogInformation("[Bedrock] Response received. Tokens: {Tokens}, Model: {Model}", tokens, _modelId);

            return new LlmResponse(text, tokens);
        }
        catch (ThrottlingException)
        {
            throw new InvalidOperationException("LLM rate limit reached. Try again in a few minutes.");
        }
        catch (ModelNotReadyException)
        {
            throw new InvalidOperationException("LLM model temporarily unavailable. Try again in a few minutes.");
        }
        catch (AccessDeniedException ex)
        {
            _logger.LogError(ex, "Bedrock access denied for model {Model}", _modelId);
            throw new InvalidOperationException("LLM configuration error. Check IAM permissions.");
        }
    }
}
