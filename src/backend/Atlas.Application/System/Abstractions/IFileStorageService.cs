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
}
