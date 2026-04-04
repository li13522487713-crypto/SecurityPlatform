using Atlas.Application.AiPlatform.Models;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IChunkingService
{
    IReadOnlyList<TextChunk> Chunk(string text, ChunkingOptions options);
}
