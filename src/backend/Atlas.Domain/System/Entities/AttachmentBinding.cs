using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.System.Entities;

/// <summary>
/// 附件绑定关系实体：将 FileRecord 与任意业务实体（EntityType + EntityId）多态关联。
/// </summary>
public sealed class AttachmentBinding : TenantEntity
{
    public AttachmentBinding()
        : base(TenantId.Empty)
    {
        EntityType = string.Empty;
    }

    public AttachmentBinding(
        TenantId tenantId,
        long fileRecordId,
        string entityType,
        long entityId,
        string? fieldKey,
        bool isPrimary,
        long id)
        : base(tenantId)
    {
        Id = id;
        FileRecordId = fileRecordId;
        EntityType = entityType;
        EntityId = entityId;
        FieldKey = fieldKey;
        IsPrimary = isPrimary;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>关联的文件记录 ID。</summary>
    public long FileRecordId { get; private set; }

    /// <summary>业务实体类型（如 DynamicTable 的 TableKey，或特定业务模块标识）。</summary>
    public string EntityType { get; private set; }

    /// <summary>业务实体记录 ID。</summary>
    public long EntityId { get; private set; }

    /// <summary>区分同一业务实体上不同逻辑附件槽（如"合同扫描件"、"审批截图"），可为空。</summary>
    public string? FieldKey { get; private set; }

    /// <summary>是否为该实体/字段槽下的主要附件。</summary>
    public bool IsPrimary { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void UpdatePrimary(bool isPrimary) => IsPrimary = isPrimary;
}
