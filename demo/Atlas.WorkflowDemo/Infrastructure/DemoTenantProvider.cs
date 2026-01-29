using Atlas.Core.Tenancy;

namespace Atlas.WorkflowDemo.Infrastructure;

/// <summary>
/// Demo租户提供者 - 提供固定的租户ID用于演示
/// </summary>
public class DemoTenantProvider : ITenantProvider
{
    /// <summary>
    /// 返回固定的租户ID（空GUID）
    /// </summary>
    public TenantId GetTenantId() => new TenantId(Guid.Empty);
}
