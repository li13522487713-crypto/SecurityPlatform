namespace Atlas.Application.AiPlatform.Abstractions;

public interface ILlmProviderFactory
{
    ILlmProvider GetLlmProvider(string? providerName = null);

    IEmbeddingProvider GetEmbeddingProvider(string? providerName = null);
}
