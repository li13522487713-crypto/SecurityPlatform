namespace Atlas.Application.AiPlatform.Models;

public sealed record ParsedDocument(
    string Text,
    string? Title,
    int PageCount,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record TextChunk(
    int Index,
    string Content,
    int StartOffset,
    int EndOffset);

public enum ChunkingStrategy
{
    Fixed = 0,
    Semantic = 1,
    Recursive = 2
}

public sealed record ChunkingOptions(
    int ChunkSize = 500,
    int Overlap = 50,
    ChunkingStrategy Strategy = ChunkingStrategy.Fixed);
