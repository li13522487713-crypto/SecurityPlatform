namespace Atlas.Application.AiPlatform.Abstractions;

public interface ILlmProviderFactory
{
    ILlmProvider GetLlmProvider(string? providerName = null);

    ILlmProvider GetLlmProviderByModelConfigId(long modelConfigId);

    IEmbeddingProvider GetEmbeddingProvider(string? providerName = null);
}
