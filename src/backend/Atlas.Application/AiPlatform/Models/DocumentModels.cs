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

/// <summary>文档解析模式：Quick 使用轻量文本解析；Precise 走完整解析管线（限时回退 Quick）。</summary>
public enum DocumentParseStrategy
{
    Quick = 0,
    Precise = 1
}

public sealed record ChunkingOptions(
    int ChunkSize = 500,
    int Overlap = 50,
    ChunkingStrategy Strategy = ChunkingStrategy.Fixed,
    DocumentParseStrategy ParseStrategy = DocumentParseStrategy.Quick);
