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
