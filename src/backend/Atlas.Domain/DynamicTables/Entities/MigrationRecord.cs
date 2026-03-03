using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.DynamicTables.Entities;

/// <summary>
/// 动态表迁移记录（版本化与执行状态跟踪）。
/// </summary>
public sealed class MigrationRecord : TenantEntity
{
    public MigrationRecord()
        : base(TenantId.Empty)
    {
        TableKey = string.Empty;
        Status = "Draft";
        UpScript = string.Empty;
    }

    public MigrationRecord(
        TenantId tenantId,
        string tableKey,
        int version,
        string upScript,
        string? downScript,
        bool isDestructive,
        long createdBy,
        long id,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        TableKey = tableKey;
        Version = version;
        Status = "Draft";
        UpScript = upScript;
        DownScript = downScript;
        IsDestructive = isDestructive;
        CreatedAt = now;
        UpdatedAt = now;
        CreatedBy = createdBy;
        UpdatedBy = createdBy;
    }

    public string TableKey { get; private set; }
    public int Version { get; private set; }
    public string Status { get; private set; }
    public string UpScript { get; private set; }
    public string? DownScript { get; private set; }
    public bool IsDestructive { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ExecutedAt { get; private set; }
    public long CreatedBy { get; private set; }
    public long UpdatedBy { get; private set; }

    public void UpdateScripts(
        string upScript,
        string? downScript,
        bool isDestructive,
        long updatedBy,
        DateTimeOffset now)
    {
        UpScript = upScript;
        DownScript = downScript;
        IsDestructive = isDestructive;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void MarkExecuting(long updatedBy, DateTimeOffset now)
    {
        Status = "Executing";
        ErrorMessage = null;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void MarkSucceeded(long updatedBy, DateTimeOffset now)
    {
        Status = "Succeeded";
        ErrorMessage = null;
        ExecutedAt = now;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void MarkFailed(string errorMessage, long updatedBy, DateTimeOffset now)
    {
        Status = "Failed";
        ErrorMessage = errorMessage;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }
}
