using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Application.LowCode.Repositories;

public interface IPromptTemplateRepository
{
    Task<long> InsertAsync(AppPromptTemplate entity, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(AppPromptTemplate entity, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<AppPromptTemplate?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<bool> ExistsCodeAsync(TenantId tenantId, string code, long? excludeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AppPromptTemplate>> SearchAsync(TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken);
}

public interface ILowCodePluginRepository
{
    Task<long> InsertDefAsync(LowCodePluginDefinition entity, CancellationToken cancellationToken);
    Task<bool> UpdateDefAsync(LowCodePluginDefinition entity, CancellationToken cancellationToken);
    Task<bool> DeleteDefAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<LowCodePluginDefinition?> FindDefByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<LowCodePluginDefinition?> FindDefByPluginIdAsync(TenantId tenantId, string pluginId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LowCodePluginDefinition>> SearchDefsAsync(TenantId tenantId, string? keyword, string? shareScope, int pageIndex, int pageSize, CancellationToken cancellationToken);

    Task<long> InsertVersionAsync(LowCodePluginVersion entity, CancellationToken cancellationToken);
    Task<long> InsertAuthAsync(LowCodePluginAuthorization entity, CancellationToken cancellationToken);
    Task<LowCodePluginAuthorization?> FindAuthAsync(TenantId tenantId, string pluginId, CancellationToken cancellationToken);

    Task<LowCodePluginUsage?> FindUsageAsync(TenantId tenantId, string pluginId, string day, CancellationToken cancellationToken);
    Task<long> InsertUsageAsync(LowCodePluginUsage entity, CancellationToken cancellationToken);
    Task<bool> UpdateUsageAsync(LowCodePluginUsage entity, CancellationToken cancellationToken);
}
