using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiAppResourceCopyTask : TenantEntity
{
    public AiAppResourceCopyTask()
        : base(TenantId.Empty)
    {
        ErrorMessage = string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public AiAppResourceCopyTask(
        TenantId tenantId,
        long appId,
        long sourceAppId,
        long id)
        : base(tenantId)
    {
        Id = id;
        AppId = appId;
        SourceAppId = sourceAppId;
        Status = AiAppResourceCopyStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long AppId { get; private set; }
    public long SourceAppId { get; private set; }
    public AiAppResourceCopyStatus Status { get; private set; }
    public int TotalItems { get; private set; }
    public int CopiedItems { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void MarkRunning()
    {
        Status = AiAppResourceCopyStatus.Running;
        UpdatedAt = DateTime.UtcNow;
        ErrorMessage = string.Empty;
    }

    public void MarkCompleted(int totalItems, int copiedItems)
    {
        Status = AiAppResourceCopyStatus.Completed;
        TotalItems = Math.Max(0, totalItems);
        CopiedItems = Math.Max(0, copiedItems);
        ErrorMessage = string.Empty;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string? errorMessage)
    {
        Status = AiAppResourceCopyStatus.Failed;
        ErrorMessage = errorMessage ?? "复制失败";
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum AiAppResourceCopyStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3
}
