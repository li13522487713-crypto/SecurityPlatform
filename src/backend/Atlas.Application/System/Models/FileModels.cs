namespace Atlas.Application.System.Models;

public sealed record FileRecordDto(
    long Id,
    string OriginalName,
    string ContentType,
    long SizeBytes,
    long UploadedById,
    string UploadedByName,
    DateTimeOffset UploadedAt);

public sealed record FileUploadResult(
    long Id,
    string OriginalName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAt);

public sealed record FileDownloadResult(
    Stream Stream,
    string ContentType,
    string FileName);

public sealed record FileChunkUploadInitRequest(
    string OriginalName,
    string ContentType,
    long TotalSizeBytes,
    int TotalParts);

public sealed record FileChunkUploadInitResult(
    long SessionId,
    int PartSizeBytes,
    DateTimeOffset ExpiresAt);

public sealed record FileChunkUploadCompleteRequest(
    string OriginalName,
    string ContentType);

public sealed record FileUploadSessionProgressDto(
    long SessionId,
    string OriginalName,
    string ContentType,
    long TotalSizeBytes,
    long UploadedSizeBytes,
    int TotalParts,
    int UploadedPartCount,
    bool Completed,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record FileSignedUrlResult(
    string Url,
    DateTimeOffset ExpiresAt);

public sealed record FileImageUploadApplyRequest(
    string OriginalName,
    string ContentType,
    long TotalSizeBytes,
    int TotalParts);

public sealed record FileImageUploadCommitRequest(
    long SessionId,
    string OriginalName,
    string ContentType);
