using System.IO;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Infrastructure.Services.AiPlatform.Parsers;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.AiPlatform;

/// <summary>
/// Quick：<see cref="TxtDocumentParser"/>；Precise：<see cref="DocumentParserComposite"/>（30s 超时回退 Quick）。
/// </summary>
public sealed class DocumentParseStrategyService : IDocumentParseStrategy
{
    private static readonly TimeSpan PreciseParseTimeout = TimeSpan.FromSeconds(30);

    private readonly TxtDocumentParser _quickParser;
    private readonly DocumentParserComposite _preciseParser;
    private readonly ILogger<DocumentParseStrategyService> _logger;

    public DocumentParseStrategyService(
        TxtDocumentParser quickParser,
        DocumentParserComposite preciseParser,
        ILogger<DocumentParseStrategyService> logger)
    {
        _quickParser = quickParser;
        _preciseParser = preciseParser;
        _logger = logger;
    }

    public async Task<ParsedDocument> ParseAsync(
        DocumentParseStrategy strategy,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken)
    {
        if (strategy == DocumentParseStrategy.Quick)
        {
            return await _quickParser.ParseAsync(stream, fileName, cancellationToken).ConfigureAwait(false);
        }

        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(PreciseParseTimeout);
        try
        {
            var result = await _preciseParser.ParseAsync(buffer, fileName, cts.Token).ConfigureAwait(false);
            return AugmentPreciseMetadata(result);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Precise document parse timed out for {FileName}; falling back to quick parser.",
                fileName);
            buffer.Position = 0;
            return await _quickParser.ParseAsync(buffer, fileName, cancellationToken).ConfigureAwait(false);
        }
    }

    private static ParsedDocument AugmentPreciseMetadata(ParsedDocument result)
    {
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (result.Metadata is not null)
        {
            foreach (var kv in result.Metadata)
            {
                meta[kv.Key] = kv.Value;
            }
        }

        meta["chartExtraction"] = """{"extracted":false,"reason":"K6 ready"}""";
        return result with { Metadata = meta };
    }
}
