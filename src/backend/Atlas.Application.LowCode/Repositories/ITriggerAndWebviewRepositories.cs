using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Application.LowCode.Repositories;

public interface ILowCodeTriggerRepository
{
    Task<long> InsertAsync(LowCodeTrigger trigger, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(LowCodeTrigger trigger, CancellationToken cancellationToken);
    Task<LowCodeTrigger?> FindByTriggerIdAsync(TenantId tenantId, string triggerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LowCodeTrigger>> ListAsync(TenantId tenantId, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(TenantId tenantId, string triggerId, CancellationToken cancellationToken);
    /// <summary>跨租户列出所有触发器（仅供启动期 Hangfire reconcile 等系统级任务使用）。</summary>
    Task<IReadOnlyList<LowCodeTrigger>> ListAllAsync(CancellationToken cancellationToken);
}

public interface ILowCodeWebviewDomainRepository
{
    Task<long> InsertAsync(LowCodeWebviewDomain domain, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(LowCodeWebviewDomain domain, CancellationToken cancellationToken);
    Task<LowCodeWebviewDomain?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<LowCodeWebviewDomain?> FindByDomainAsync(TenantId tenantId, string domain, CancellationToken cancellationToken);
    Task<IReadOnlyList<LowCodeWebviewDomain>> ListAsync(TenantId tenantId, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
}
