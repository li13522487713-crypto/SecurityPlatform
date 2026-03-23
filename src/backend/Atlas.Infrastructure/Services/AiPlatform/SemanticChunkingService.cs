using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class SemanticChunkingService : IChunkingStrategy
{
    private static readonly string[] ParagraphBreaks = ["\r\n\r\n", "\n\n"];
    private static readonly char[] SentenceSeparators = ['。', '！', '？', '.', '!', '?', '\n'];

    public ChunkingStrategy Strategy => ChunkingStrategy.Semantic;

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

        var segments = SplitParagraphs(text);
        var chunks = new List<TextChunk>();
        var chunkIndex = 0;
        var currentStart = 0;

        foreach (var segment in segments)
        {
            var normalizedSegment = segment.Trim();
            if (normalizedSegment.Length == 0)
            {
                continue;
            }

            if (normalizedSegment.Length <= options.ChunkSize)
            {
                chunks.Add(new TextChunk(chunkIndex++, normalizedSegment, currentStart, currentStart + normalizedSegment.Length));
                currentStart += normalizedSegment.Length;
                continue;
            }

            foreach (var sentenceChunk in SplitBySentence(normalizedSegment, options))
            {
                chunks.Add(new TextChunk(chunkIndex++, sentenceChunk.Content, currentStart + sentenceChunk.StartOffset, currentStart + sentenceChunk.EndOffset));
            }

            currentStart += normalizedSegment.Length;
        }

        return chunks;
    }

    private static IEnumerable<string> SplitParagraphs(string text)
    {
        var segments = new List<string> { text };
        foreach (var separator in ParagraphBreaks)
        {
            segments = segments
                .SelectMany(segment => segment.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToList();
        }

        return segments;
    }

    private static IReadOnlyList<TextChunk> SplitBySentence(string text, ChunkingOptions options)
    {
        var chunks = new List<TextChunk>();
        var offset = 0;
        var index = 0;
        while (offset < text.Length)
        {
            var targetEnd = Math.Min(offset + options.ChunkSize, text.Length);
            var end = FindSentenceBoundary(text, offset, targetEnd);
            if (end <= offset)
            {
                end = targetEnd;
            }

            var content = text[offset..end].Trim();
            if (!string.IsNullOrWhiteSpace(content))
            {
                chunks.Add(new TextChunk(index++, content, offset, end));
            }

            if (end >= text.Length)
            {
                break;
            }

            offset = Math.Max(0, end - options.Overlap);
        }

        return chunks;
    }

    private static int FindSentenceBoundary(string text, int start, int targetEnd)
    {
        for (var i = targetEnd; i < text.Length && i < targetEnd + 80; i++)
        {
            if (SentenceSeparators.Contains(text[i]))
            {
                return i + 1;
            }
        }

        for (var i = Math.Min(targetEnd - 1, text.Length - 1); i >= start && i > targetEnd - 80; i--)
        {
            if (SentenceSeparators.Contains(text[i]))
            {
                return i + 1;
            }
        }

        return targetEnd;
    }
}
