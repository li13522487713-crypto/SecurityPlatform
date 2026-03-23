using Atlas.Application.AiPlatform.Models;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IChunkingStrategy
{
    ChunkingStrategy Strategy { get; }

    IReadOnlyList<TextChunk> Chunk(string text, ChunkingOptions options);
}
