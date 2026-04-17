using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

public interface IRuntimeTriggerService
{
    Task<IReadOnlyList<TriggerInfoDto>> ListAsync(TenantId tenantId, CancellationToken cancellationToken);
    Task<TriggerInfoDto> UpsertAsync(TenantId tenantId, long currentUserId, TriggerUpsertRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long currentUserId, string triggerId, CancellationToken cancellationToken);
    Task PauseAsync(TenantId tenantId, long currentUserId, string triggerId, CancellationToken cancellationToken);
    Task ResumeAsync(TenantId tenantId, long currentUserId, string triggerId, CancellationToken cancellationToken);
    /// <summary>由 Hangfire CRON 调度器调用：执行触发动作。</summary>
    Task FireAsync(TenantId tenantId, string triggerId, CancellationToken cancellationToken);
}

public interface IRuntimeWebviewDomainService
{
    Task<IReadOnlyList<WebviewDomainInfoDto>> ListAsync(TenantId tenantId, CancellationToken cancellationToken);
    Task<WebviewDomainInfoDto> AddAsync(TenantId tenantId, long currentUserId, AddWebviewDomainRequest request, CancellationToken cancellationToken);
    Task<WebviewDomainInfoDto> VerifyAsync(TenantId tenantId, long currentUserId, long id, CancellationToken cancellationToken);
    Task RemoveAsync(TenantId tenantId, long currentUserId, long id, CancellationToken cancellationToken);
}
