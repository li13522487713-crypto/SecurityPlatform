using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiDatabaseImportTask : TenantEntity
{
    public AiDatabaseImportTask()
        : base(TenantId.Empty)
    {
        ErrorMessage = string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public AiDatabaseImportTask(
        TenantId tenantId,
        long databaseId,
        long fileId,
        long id)
        : base(tenantId)
    {
        Id = id;
        DatabaseId = databaseId;
        FileId = fileId;
        Status = AiDatabaseImportStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long DatabaseId { get; private set; }
    public long FileId { get; private set; }
    public AiDatabaseImportStatus Status { get; private set; }
    public int TotalRows { get; private set; }
    public int SucceededRows { get; private set; }
    public int FailedRows { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void MarkRunning()
    {
        Status = AiDatabaseImportStatus.Running;
        UpdatedAt = DateTime.UtcNow;
        ErrorMessage = string.Empty;
    }

    public void MarkCompleted(int totalRows, int succeededRows, int failedRows)
    {
        Status = AiDatabaseImportStatus.Completed;
        TotalRows = Math.Max(0, totalRows);
        SucceededRows = Math.Max(0, succeededRows);
        FailedRows = Math.Max(0, failedRows);
        ErrorMessage = string.Empty;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string? errorMessage)
    {
        Status = AiDatabaseImportStatus.Failed;
        ErrorMessage = errorMessage ?? "导入失败";
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum AiDatabaseImportStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3
}
