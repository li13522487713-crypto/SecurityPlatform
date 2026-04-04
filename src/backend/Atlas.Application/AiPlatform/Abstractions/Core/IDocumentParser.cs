using Atlas.Application.AiPlatform.Models;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IDocumentParser
{
    bool CanParse(string? contentType, string extension);

    Task<ParsedDocument> ParseAsync(Stream fileStream, string fileName, CancellationToken ct = default);
}
