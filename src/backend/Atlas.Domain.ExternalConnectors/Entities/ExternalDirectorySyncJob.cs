using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Domain.ExternalConnectors.Entities;

/// <summary>
/// 通讯录同步任务（全量 / 增量）记录。Diff 行单独存放在 ExternalDirectorySyncDiff，便于对账面板分页展示。
/// </summary>
public sealed class ExternalDirectorySyncJob : TenantEntity
{
    public ExternalDirectorySyncJob()
        : base(TenantId.Empty)
    {
        TriggerSource = string.Empty;
    }

    public ExternalDirectorySyncJob(
        TenantId tenantId,
        long id,
        long providerId,
        DirectorySyncJobMode mode,
        string triggerSource,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        ProviderId = providerId;
        Mode = mode;
        Status = DirectorySyncJobStatus.Pending;
        TriggerSource = triggerSource;
        StartedAt = now;
        CreatedAt = now;
    }

    public long ProviderId { get; private set; }

    public DirectorySyncJobMode Mode { get; private set; }

    public DirectorySyncJobStatus Status { get; private set; }

    /// <summary>"manual" / "cron" / "webhook" / "system"。</summary>
    public string TriggerSource { get; private set; }

    public int DepartmentCreated { get; private set; }

    public int DepartmentUpdated { get; private set; }

    public int DepartmentDeleted { get; private set; }

    public int UserCreated { get; private set; }

    public int UserUpdated { get; private set; }

    public int UserDeleted { get; private set; }

    public int RelationChanged { get; private set; }

    public int FailedItems { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void MarkRunning()
    {
        Status = DirectorySyncJobStatus.Running;
    }

    public void Accumulate(DirectorySyncDiffType type)
    {
        switch (type)
        {
            case DirectorySyncDiffType.DepartmentCreated: DepartmentCreated++; break;
            case DirectorySyncDiffType.DepartmentUpdated: DepartmentUpdated++; break;
            case DirectorySyncDiffType.DepartmentDeleted: DepartmentDeleted++; break;
            case DirectorySyncDiffType.UserCreated: UserCreated++; break;
            case DirectorySyncDiffType.UserUpdated: UserUpdated++; break;
            case DirectorySyncDiffType.UserDeleted: UserDeleted++; break;
            case DirectorySyncDiffType.RelationCreated:
            case DirectorySyncDiffType.RelationDeleted:
                RelationChanged++;
                break;
        }
    }

    public void IncrementFailed()
    {
        FailedItems++;
    }

    public void Complete(DateTimeOffset now)
    {
        Status = FailedItems == 0 ? DirectorySyncJobStatus.Succeeded : DirectorySyncJobStatus.PartialSucceeded;
        FinishedAt = now;
    }

    public void Fail(string errorMessage, DateTimeOffset now)
    {
        Status = DirectorySyncJobStatus.Failed;
        ErrorMessage = errorMessage;
        FinishedAt = now;
    }

    public void Cancel(DateTimeOffset now)
    {
        Status = DirectorySyncJobStatus.Canceled;
        FinishedAt = now;
    }
}
