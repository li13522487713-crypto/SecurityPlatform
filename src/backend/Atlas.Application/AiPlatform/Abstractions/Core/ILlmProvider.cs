using Atlas.Application.AiPlatform.Models;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface ILlmProvider
{
    string ProviderName { get; }

    Task<ChatCompletionResult> ChatAsync(ChatCompletionRequest request, CancellationToken ct = default);

    IAsyncEnumerable<ChatCompletionChunk> ChatStreamAsync(ChatCompletionRequest request, CancellationToken ct = default);
}
