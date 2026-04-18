using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

public interface IRuntimeFileService
{
    Task<RuntimeFilePrepareUploadResponse> PrepareAsync(TenantId tenantId, long currentUserId, string currentUserName, RuntimeFilePrepareUploadRequest request, CancellationToken cancellationToken);
    Task<RuntimeFileCompleteUploadResponse> CompleteAsync(TenantId tenantId, long currentUserId, string currentUserName, string token, Stream content, long contentLength, CancellationToken cancellationToken);
    Task CancelAsync(TenantId tenantId, long currentUserId, string token, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long currentUserId, string fileHandle, CancellationToken cancellationToken);
    /// <summary>GC：标记过期上传会话；以及对未引用 fileHandle 做软删除（7 天窗口由 Hangfire 调度）。</summary>
    Task<int> RunGarbageCollectionAsync(CancellationToken cancellationToken);
}
