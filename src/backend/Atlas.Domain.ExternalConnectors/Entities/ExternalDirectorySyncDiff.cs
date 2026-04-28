using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Domain.ExternalConnectors.Entities;

/// <summary>
/// 单条同步差异行（一个外部对象 / 一段补丁）。失败行带 ErrorMessage，由对账面板展示并允许人工重试。
/// </summary>
public sealed class ExternalDirectorySyncDiff : TenantEntity
{
    public ExternalDirectorySyncDiff()
        : base(TenantId.Empty)
    {
        EntityId = string.Empty;
        Summary = string.Empty;
    }

    public ExternalDirectorySyncDiff(
        TenantId tenantId,
        long id,
        long jobId,
        long providerId,
        DirectorySyncDiffType diffType,
        string entityId,
        string summary,
        string? errorMessage,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        JobId = jobId;
        ProviderId = providerId;
        DiffType = diffType;
        EntityId = entityId;
        Summary = summary;
        ErrorMessage = errorMessage;
        OccurredAt = now;
    }

    public long JobId { get; private set; }

    public long ProviderId { get; private set; }

    public DirectorySyncDiffType DiffType { get; private set; }

    /// <summary>变更的实体 ID（部门 ID 或 用户 ID）。</summary>
    public string EntityId { get; private set; }

    /// <summary>简短摘要（适合对账面板列表直接展示）。</summary>
    public string Summary { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}
