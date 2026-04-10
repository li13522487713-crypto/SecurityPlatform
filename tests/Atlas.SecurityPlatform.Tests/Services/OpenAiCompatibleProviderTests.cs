using System.Net;
using System.Text;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Services.AiPlatform;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Atlas.SecurityPlatform.Tests.Services;

public sealed class OpenAiCompatibleProviderTests
{
    [Fact]
    public async Task ChatAsync_ShouldUseProviderDefaultBaseUrl_WhenBaseUrlIsEmpty()
    {
        var handler = new CaptureHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"model":"gpt-4o-mini","choices":[{"message":{"content":"ok"},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}""",
                    Encoding.UTF8,
                    "application/json")
            });
        var provider = CreateProvider("openai", string.Empty, handler);

        var result = await provider.ChatAsync(new ChatCompletionRequest(
            "gpt-4o-mini",
            [new ChatMessage("user", "hello")]));

        Assert.Equal("ok", result.Content);
        Assert.Equal("https://api.openai.com/v1/chat/completions", handler.LastRequestUri?.ToString());
    }

    [Fact]
    public async Task ChatAsync_ShouldThrowClearError_WhenProviderBaseUrlCannotBeResolved()
    {
        var handler = new CaptureHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var provider = CreateProvider("custom", string.Empty, handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.ChatAsync(new ChatCompletionRequest(
            "custom-model",
            [new ChatMessage("user", "hello")])));

        Assert.Contains("缺少有效的 BaseUrl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChatAsync_ShouldUseRequestEndpointOverride_WhenProviderBaseUrlIsEmpty()
    {
        var handler = new CaptureHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"model":"deepseek-chat","choices":[{"message":{"content":"ok"},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}""",
                    Encoding.UTF8,
                    "application/json")
            });
        var provider = CreateProvider(string.Empty, string.Empty, handler);

        var result = await provider.ChatAsync(new ChatCompletionRequest(
            "deepseek-chat",
            [new ChatMessage("user", "hello")],
            Endpoint: "https://api.deepseek.com/v1"));

        Assert.Equal("ok", result.Content);
        Assert.Equal("https://api.deepseek.com/v1/chat/completions", handler.LastRequestUri?.ToString());
    }

    private static OpenAiCompatibleProvider CreateProvider(string providerName, string baseUrl, HttpMessageHandler handler)
    {
        return new OpenAiCompatibleProvider(
            providerName,
            new AiProviderOption
            {
                BaseUrl = baseUrl,
                ApiKey = "test-key",
                DefaultModel = "test-model"
            },
            new HttpClient(handler),
            TenantId.Empty,
            Substitute.For<IMeteringService>(),
            Substitute.For<ILogger<OpenAiCompatibleProvider>>());
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public CaptureHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(_responder(request));
        }
    }
}
