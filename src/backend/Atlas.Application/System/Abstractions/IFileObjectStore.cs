using Atlas.Core.Tenancy;

namespace Atlas.Application.System.Abstractions;

/// <summary>
/// 文件对象存储抽象（支持 Local/MinIO/OSS 等多后端）。
/// </summary>
public interface IFileObjectStore
{
    /// <summary>
    /// 保存对象内容。
    /// </summary>
    Task SaveAsync(
        TenantId tenantId,
        string objectName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 打开对象读取流。
    /// </summary>
    Task<Stream> OpenReadAsync(
        TenantId tenantId,
        string objectName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 判断对象是否存在。
    /// </summary>
    Task<bool> ExistsAsync(
        TenantId tenantId,
        string objectName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除对象。
    /// </summary>
    Task DeleteAsync(
        TenantId tenantId,
        string objectName,
        CancellationToken cancellationToken = default);
}
