using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class ChunkingService : IChunkingService
{
    private readonly IReadOnlyDictionary<ChunkingStrategy, IChunkingStrategy> _strategyMap;

    public ChunkingService(IEnumerable<IChunkingStrategy> strategies)
    {
        _strategyMap = strategies.ToDictionary(strategy => strategy.Strategy);
    }

    public IReadOnlyList<TextChunk> Chunk(string text, ChunkingOptions options)
    {
        if (!_strategyMap.TryGetValue(options.Strategy, out var strategy))
        {
            strategy = _strategyMap[ChunkingStrategy.Fixed];
        }

        return strategy.Chunk(text, options);
    }
}
