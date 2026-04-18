using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Domain.ExternalConnectors.Entities;

/// <summary>
/// 身份绑定的全量审计日志：记录自动绑定 / 换号 / 重名 / 解绑等关键事件。
/// 不可由业务方法外部修改，仅由 IExternalIdentityBindingService 写入。
/// </summary>
public sealed class ExternalIdentityBindingAuditLog : TenantEntity
{
    public ExternalIdentityBindingAuditLog()
        : base(TenantId.Empty)
    {
        ExternalUserId = string.Empty;
        Detail = string.Empty;
    }

    public ExternalIdentityBindingAuditLog(
        TenantId tenantId,
        long id,
        long providerId,
        long? bindingId,
        long? localUserId,
        string externalUserId,
        IdentityBindingAuditAction action,
        string detail,
        string? actor,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        ProviderId = providerId;
        BindingId = bindingId;
        LocalUserId = localUserId;
        ExternalUserId = externalUserId;
        Action = action;
        Detail = detail;
        Actor = actor;
        OccurredAt = now;
    }

    public long ProviderId { get; private set; }

    public long? BindingId { get; private set; }

    public long? LocalUserId { get; private set; }

    public string ExternalUserId { get; private set; }

    public IdentityBindingAuditAction Action { get; private set; }

    /// <summary>JSON 字符串：包含 strategy / 旧值 / 新值 / 冲突的另一条 binding 等。</summary>
    public string Detail { get; private set; }

    /// <summary>操作来源："system" / "user:{id}" / "admin:{id}"。</summary>
    public string? Actor { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}
