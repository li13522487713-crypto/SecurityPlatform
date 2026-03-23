using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Application.AiPlatform.Models;

public sealed record MultimodalAssetCreateRequest(
    MultimodalAssetType AssetType,
    MultimodalSourceType SourceType,
    string? Name,
    string? MimeType,
    string? FileId,
    string? SourceUrl,
    string? ContentText,
    string? MetadataJson);

public sealed record MultimodalAssetDto(
    long Id,
    MultimodalAssetType AssetType,
    MultimodalSourceType SourceType,
    MultimodalAssetStatus Status,
    string Name,
    string MimeType,
    string FileId,
    string SourceUrl,
    string? ContentText,
    string MetadataJson,
    long CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record VisionAnalyzeRequest(
    long? AssetId,
    string? ImageUrl,
    string? Prompt);

public sealed record VisionAnalyzeResult(
    string Summary,
    string? DetectedText,
    string Provider,
    long? AssetId);

public sealed record AsrTranscribeRequest(
    long? AssetId,
    string? AudioUrl,
    string? LanguageHint,
    string? Prompt);

public sealed record AsrTranscribeResult(
    string Transcript,
    string Provider,
    long? AssetId);

public sealed record TtsSynthesizeRequest(
    string Text,
    string? Voice,
    string? Format,
    string? Language);

public sealed record TtsSynthesizeResult(
    string Provider,
    string AudioDataUri,
    string MimeType,
    int DurationSecondsEstimate);
