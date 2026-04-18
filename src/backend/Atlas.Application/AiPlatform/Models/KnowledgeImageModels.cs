namespace Atlas.Application.AiPlatform.Models;

public enum KnowledgeImageAnnotationType
{
    Tag = 0,
    Caption = 1,
    Ocr = 2,
    Vlm = 3
}

public sealed record KnowledgeImageAnnotationDto(
    long Id,
    long ImageItemId,
    KnowledgeImageAnnotationType Type,
    string Text,
    float? Confidence = null);

public sealed record KnowledgeImageItemDto(
    long Id,
    long KnowledgeBaseId,
    long DocumentId,
    string FileName,
    IReadOnlyList<KnowledgeImageAnnotationDto> Annotations,
    long? FileId = null,
    int? Width = null,
    int? Height = null,
    string? ThumbnailUrl = null);
