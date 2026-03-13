using System.Text;
using System.Text.RegularExpressions;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;

namespace Atlas.Infrastructure.Services.AiPlatform.Parsers;

public sealed partial class MarkdownDocumentParser : IDocumentParser
{
    public bool CanParse(string? contentType, string extension)
    {
        return string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase)
               || string.Equals(extension, ".markdown", StringComparison.OrdinalIgnoreCase)
               || string.Equals(contentType, "text/markdown", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ParsedDocument> ParseAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var markdown = await reader.ReadToEndAsync(ct);
        var text = StripMarkdown(markdown);

        return new ParsedDocument(
            text,
            Path.GetFileNameWithoutExtension(fileName),
            1,
            new Dictionary<string, string>
            {
                ["fileName"] = fileName,
                ["parser"] = nameof(MarkdownDocumentParser)
            });
    }

    private static string StripMarkdown(string markdown)
    {
        var value = markdown;
        value = HeaderRegex().Replace(value, string.Empty);
        value = CodeFenceRegex().Replace(value, string.Empty);
        value = ImageRegex().Replace(value, "$1");
        value = LinkRegex().Replace(value, "$1");
        value = EmphasisRegex().Replace(value, "$1");
        value = ListPrefixRegex().Replace(value, string.Empty);
        return value.Trim();
    }

    [GeneratedRegex(@"(?m)^\s{0,3}#{1,6}\s*")]
    private static partial Regex HeaderRegex();

    [GeneratedRegex("```[\\s\\S]*?```")]
    private static partial Regex CodeFenceRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)]+)\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"!\[([^\]]*)\]\(([^)]+)\)")]
    private static partial Regex ImageRegex();

    [GeneratedRegex(@"[*_~`]{1,3}([^*_~`]+)[*_~`]{1,3}")]
    private static partial Regex EmphasisRegex();

    [GeneratedRegex(@"(?m)^\s*[-*+]\s+")]
    private static partial Regex ListPrefixRegex();
}
