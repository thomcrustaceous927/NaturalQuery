using System.Net;
using System.Text;
using FluentAssertions;
using NaturalQuery.Providers;

namespace NaturalQuery.Tests;

public class OpenAiProviderTests
{
    private class MockHttpHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        private readonly HttpStatusCode _statusCode;
        private readonly Exception? _exception;

        public MockHttpHandler(string responseBody, HttpStatusCode statusCode)
        {
            _responseBody = responseBody;
            _statusCode = statusCode;
        }

        public MockHttpHandler(Exception exception)
        {
            _responseBody = "";
            _statusCode = HttpStatusCode.OK;
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_exception != null)
                throw _exception;

            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            });
        }
    }

    private static OpenAiProvider CreateProvider(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new MockHttpHandler(responseJson, statusCode);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/") };
        return new OpenAiProvider(client, "test-key", "gpt-4o-mini");
    }

    private static OpenAiProvider CreateProviderWithException(Exception ex)
    {
        var handler = new MockHttpHandler(ex);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/") };
        return new OpenAiProvider(client, "test-key", "gpt-4o-mini");
    }

    [Fact]
    public async Task Successful_Response_Should_Return_Text_And_Tokens()
    {
        var responseJson = """
        {
            "choices": [{ "message": { "content": "SELECT * FROM users" } }],
            "usage": { "prompt_tokens": 10, "completion_tokens": 20 }
        }
        """;

        var provider = CreateProvider(responseJson);
        var result = await provider.GenerateAsync("system prompt", "user prompt");

        result.Text.Should().Be("SELECT * FROM users");
        result.TokensUsed.Should().Be(30);
    }

    [Fact]
    public async Task Rate_Limit_429_Should_Throw_InvalidOperationException()
    {
        var provider = CreateProvider("""{"error":{"message":"rate limited"}}""", HttpStatusCode.TooManyRequests);

        var act = () => provider.GenerateAsync("system", "user");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*rate limit*");
    }

    [Fact]
    public async Task Server_Error_500_Should_Throw_InvalidOperationException()
    {
        var provider = CreateProvider("""{"error":{"message":"internal error"}}""", HttpStatusCode.InternalServerError);

        var act = () => provider.GenerateAsync("system", "user");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*OpenAI API error*");
    }

    [Fact]
    public async Task Network_Error_Should_Throw_InvalidOperationException()
    {
        var provider = CreateProviderWithException(new HttpRequestException("Connection refused"));

        var act = () => provider.GenerateAsync("system", "user");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to connect*");
    }

    [Fact]
    public async Task Unauthorized_401_Should_Throw_InvalidOperationException()
    {
        var provider = CreateProvider("""{"error":{"message":"invalid api key"}}""", HttpStatusCode.Unauthorized);

        var act = () => provider.GenerateAsync("system", "user");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*OpenAI API error*");
    }
}
