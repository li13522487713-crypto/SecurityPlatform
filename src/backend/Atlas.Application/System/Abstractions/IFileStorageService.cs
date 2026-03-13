using Atlas.Application.System.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.System.Abstractions;

public interface IFileStorageService
{
    /// <summary>
    /// 上传文件：验证类型/大小 → 存储物理文件 → 写入 FileRecord。
    /// </summary>
    Task<FileUploadResult> UploadAsync(
        TenantId tenantId,
        long uploadedById,
        string uploadedByName,
        string originalName,
        string contentType,
        Stream fileStream,
        long fileSizeBytes,
        CancellationToken ct = default);

    /// <summary>
    /// 下载文件：验证租户归属 → 返回文件流。
    /// </summary>
    Task<FileDownloadResult> DownloadAsync(TenantId tenantId, long fileId, CancellationToken ct = default);

    /// <summary>
    /// 获取文件元数据。
    /// </summary>
    Task<FileRecordDto?> GetInfoAsync(TenantId tenantId, long fileId, CancellationToken ct = default);

    /// <summary>
    /// 软删除文件记录（等保2.0：保留可查审计链，物理文件延迟清理）。
    /// </summary>
    Task DeleteAsync(TenantId tenantId, long fileId, CancellationToken ct = default);

    /// <summary>
    /// 初始化分片上传会话。
    /// </summary>
    Task<FileChunkUploadInitResult> InitChunkUploadAsync(
        TenantId tenantId,
        long uploadedById,
        string uploadedByName,
        FileChunkUploadInitRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// 上传指定分片。
    /// </summary>
    Task UploadChunkPartAsync(
        TenantId tenantId,
        long sessionId,
        int partNumber,
        Stream partStream,
        long partSizeBytes,
        CancellationToken ct = default);

    /// <summary>
    /// 完成分片上传并生成文件记录。
    /// </summary>
    Task<FileUploadResult> CompleteChunkUploadAsync(
        TenantId tenantId,
        long sessionId,
        FileChunkUploadCompleteRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// 查询上传会话进度。
    /// </summary>
    Task<FileUploadSessionProgressDto?> GetChunkUploadProgressAsync(
        TenantId tenantId,
        long sessionId,
        CancellationToken ct = default);

    /// <summary>
    /// 生成短期签名下载地址。
    /// </summary>
    Task<FileSignedUrlResult> GenerateSignedUrlAsync(
        TenantId tenantId,
        long fileId,
        int expiresInSeconds,
        CancellationToken ct = default);

    /// <summary>
    /// 使用签名信息下载文件。
    /// </summary>
    Task<FileDownloadResult> DownloadBySignatureAsync(
        TenantId tenantId,
        long fileId,
        long expiresUnixSeconds,
        string signature,
        CancellationToken ct = default);
}
