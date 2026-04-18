using Atlas.Application.AiPlatform.Models;

namespace Atlas.Application.AiPlatform.Abstractions;

/// <summary>
/// 按 <see cref="DocumentParseStrategy"/> 选择解析管线（quick / precise）。
/// </summary>
public interface IDocumentParseStrategy
{
    Task<ParsedDocument> ParseAsync(
        DocumentParseStrategy strategy,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken);
}
