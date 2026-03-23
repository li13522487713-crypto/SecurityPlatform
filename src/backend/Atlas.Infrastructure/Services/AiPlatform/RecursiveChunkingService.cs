using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class RecursiveChunkingService : IChunkingStrategy
{
    private static readonly string[] Separators = ["\n\n", "\n", "。", "！", "？", ".", "!", "?", " ", ""];

    public ChunkingStrategy Strategy => ChunkingStrategy.Recursive;

    public IReadOnlyList<TextChunk> Chunk(string text, ChunkingOptions options)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        if (options.ChunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.ChunkSize), "ChunkSize must be greater than zero.");
        }

        if (options.Overlap < 0 || options.Overlap >= options.ChunkSize)
        {
            throw new ArgumentOutOfRangeException(nameof(options.Overlap), "Overlap must be in range [0, ChunkSize).");
        }

        var segments = SplitRecursively(text, options.ChunkSize, 0);
        var chunks = new List<TextChunk>();
        var index = 0;
        var offset = 0;
        foreach (var segment in segments)
        {
            var content = segment.Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            chunks.Add(new TextChunk(index++, content, offset, offset + content.Length));
            offset = Math.Max(0, offset + content.Length - options.Overlap);
        }

        return chunks;
    }

    private static IReadOnlyList<string> SplitRecursively(string text, int chunkSize, int separatorIndex)
    {
        if (text.Length <= chunkSize)
        {
            return [text];
        }

        if (separatorIndex >= Separators.Length)
        {
            return HardSplit(text, chunkSize);
        }

        var separator = Separators[separatorIndex];
        if (string.IsNullOrEmpty(separator))
        {
            return HardSplit(text, chunkSize);
        }

        var parts = text.Split(separator, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1)
        {
            return SplitRecursively(text, chunkSize, separatorIndex + 1);
        }

        var merged = new List<string>();
        var current = string.Empty;
        foreach (var part in parts)
        {
            var candidate = string.IsNullOrEmpty(current) ? part : $"{current}{separator}{part}";
            if (candidate.Length <= chunkSize)
            {
                current = candidate;
                continue;
            }

            if (!string.IsNullOrEmpty(current))
            {
                merged.Add(current);
            }

            if (part.Length <= chunkSize)
            {
                current = part;
            }
            else
            {
                merged.AddRange(SplitRecursively(part, chunkSize, separatorIndex + 1));
                current = string.Empty;
            }
        }

        if (!string.IsNullOrEmpty(current))
        {
            merged.Add(current);
        }

        return merged;
    }

    private static IReadOnlyList<string> HardSplit(string text, int chunkSize)
    {
        var segments = new List<string>();
        var offset = 0;
        while (offset < text.Length)
        {
            var end = Math.Min(offset + chunkSize, text.Length);
            segments.Add(text[offset..end]);
            offset = end;
        }

        return segments;
    }
}
