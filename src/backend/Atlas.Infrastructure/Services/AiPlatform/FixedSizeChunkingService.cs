using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class FixedSizeChunkingService : IChunkingStrategy
{
    private static readonly char[] SentenceBoundaries = ['.', '!', '?', '。', '！', '？', '\n'];

    public ChunkingStrategy Strategy => ChunkingStrategy.Fixed;

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

        var chunks = new List<TextChunk>();
        var index = 0;
        var offset = 0;
        while (offset < text.Length)
        {
            var targetEnd = Math.Min(offset + options.ChunkSize, text.Length);
            var end = FindBoundary(text, offset, targetEnd);
            if (end <= offset)
            {
                end = targetEnd;
            }

            var content = text[offset..end].Trim();
            if (!string.IsNullOrWhiteSpace(content))
            {
                chunks.Add(new TextChunk(index, content, offset, end));
                index++;
            }

            if (end >= text.Length)
            {
                break;
            }

            offset = Math.Max(0, end - options.Overlap);
        }

        return chunks;
    }

    private static int FindBoundary(string text, int start, int targetEnd)
    {
        var windowLeft = Math.Max(start, targetEnd - 50);
        var windowRight = Math.Min(text.Length, targetEnd + 50);
        var forwardStart = Math.Min(targetEnd, text.Length - 1);

        for (var i = forwardStart; i < windowRight; i++)
        {
            if (SentenceBoundaries.Contains(text[i]))
            {
                return i + 1;
            }
        }

        var backwardStart = Math.Min(targetEnd - 1, text.Length - 1);
        for (var i = backwardStart; i >= windowLeft; i--)
        {
            if (SentenceBoundaries.Contains(text[i]))
            {
                return i + 1;
            }
        }

        return targetEnd;
    }
}
