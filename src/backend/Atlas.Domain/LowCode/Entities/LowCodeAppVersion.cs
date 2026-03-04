using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 低代码应用版本快照（发布/回滚）。
/// </summary>
public sealed class LowCodeAppVersion : TenantEntity
{
    public LowCodeAppVersion()
        : base(TenantId.Empty)
    {
        SnapshotJson = string.Empty;
        ActionType = string.Empty;
    }

    public LowCodeAppVersion(
        TenantId tenantId,
        long appId,
        int version,
        string snapshotJson,
        string actionType,
        long createdBy,
        long id,
        DateTimeOffset now,
        long? sourceVersionId = null,
        string? note = null)
        : base(tenantId)
    {
        Id = id;
        AppId = appId;
        Version = version;
        SnapshotJson = snapshotJson;
        ActionType = actionType;
        SourceVersionId = sourceVersionId;
        Note = note;
        CreatedAt = now;
        CreatedBy = createdBy;
    }

    /// <summary>所属应用 ID</summary>
    public long AppId { get; private set; }

    /// <summary>快照版本号</summary>
    public int Version { get; private set; }

    /// <summary>快照内容 JSON</summary>
    public string SnapshotJson { get; private set; }

    /// <summary>动作类型（Publish/Rollback）</summary>
    public string ActionType { get; private set; }

    /// <summary>回滚来源版本 ID（仅 Rollback 时有值）</summary>
    public long? SourceVersionId { get; private set; }

    /// <summary>备注</summary>
    public string? Note { get; private set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>创建人 ID</summary>
    public long CreatedBy { get; private set; }
}
