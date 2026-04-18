namespace Atlas.Application.LowCode.Models;

/// <summary>
/// 低代码运行时文件 DTO 集（M10 S10-1）。
/// 与前端 @atlas/lowcode-asset-adapter 完全对齐。
/// </summary>
public sealed record RuntimeFilePrepareUploadRequest(
    string FileName,
    string ContentType,
    long Size,
    string? Sha256);

public sealed record RuntimeFilePrepareUploadResponse(
    string Token,
    string UploadUrl,
    bool? InstantHit,
    string? FileHandle);

public sealed record RuntimeFileCompleteUploadResponse(
    string FileHandle,
    string Url,
    string ContentType,
    long Size,
    string? ImageId);
