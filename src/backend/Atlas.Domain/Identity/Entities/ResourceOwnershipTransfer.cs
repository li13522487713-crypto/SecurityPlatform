using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Identity.Entities;

/// <summary>
/// 治理 M-G06-C3（S12）：资源所有权移交记录。
/// 用于离职 / 角色变更场景下把某用户名下的资源批量移交给继任人。
/// 状态机：planned -> executed | failed。
/// </summary>
[SugarTable("ResourceOwnershipTransfer")]
public sealed class ResourceOwnershipTransfer : TenantEntity
{
    public const string StatusPlanned = "planned";
    public const string StatusExecuted = "executed";
    public const string StatusFailed = "failed";

    public ResourceOwnershipTransfer()
        : base(TenantId.Empty)
    {
        ResourceType = string.Empty;
        Status = StatusPlanned;
        CreatedAt = DateTime.UtcNow;
        Notes = string.Empty;
    }

    public ResourceOwnershipTransfer(
        TenantId tenantId,
        string resourceType,
        long resourceId,
        long fromUserId,
        long toUserId,
        long initiatedBy,
        string? notes,
        long id)
        : base(tenantId)
    {
        Id = id;
        ResourceType = resourceType.Trim().ToLowerInvariant();
        ResourceId = resourceId;
        FromUserId = fromUserId;
        ToUserId = toUserId;
        InitiatedBy = initiatedBy;
        Notes = notes ?? string.Empty;
        Status = StatusPlanned;
        CreatedAt = DateTime.UtcNow;
    }

    [SugarColumn(Length = 32, IsNullable = false)]
    public string ResourceType { get; private set; }
    public long ResourceId { get; private set; }
    public long FromUserId { get; private set; }
    public long ToUserId { get; private set; }
    public long InitiatedBy { get; private set; }

    [SugarColumn(Length = 16, IsNullable = false)]
    public string Status { get; private set; }

    [SugarColumn(Length = 512, IsNullable = false)]
    public string Notes { get; private set; }

    public DateTime CreatedAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? ExecutedAt { get; private set; }

    public void MarkExecuted()
    {
        Status = StatusExecuted;
        ExecutedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        Status = StatusFailed;
        Notes = reason;
        ExecutedAt = DateTime.UtcNow;
    }
}
