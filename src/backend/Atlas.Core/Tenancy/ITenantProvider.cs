namespace Atlas.Core.Tenancy;

public interface ITenantProvider
{
    TenantId GetTenantId();

    /// <summary>
    /// 兼容旧调用方式：_tenantProvider.TenantId
    /// </summary>
    TenantId TenantId => GetTenantId();
}