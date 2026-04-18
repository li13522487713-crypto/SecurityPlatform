using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

public interface IPromptTemplateService
{
    Task<long> UpsertAsync(TenantId tenantId, long currentUserId, PromptTemplateUpsertRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long currentUserId, long id, CancellationToken cancellationToken);
    Task<IReadOnlyList<PromptTemplateDto>> SearchAsync(TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken);
    Task<PromptTemplateDto?> GetAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
}

public interface ILowCodePluginService
{
    Task<long> UpsertDefAsync(TenantId tenantId, long currentUserId, PluginUpsertRequest request, CancellationToken cancellationToken);
    Task DeleteDefAsync(TenantId tenantId, long currentUserId, long id, CancellationToken cancellationToken);
    Task<IReadOnlyList<PluginDefinitionDto>> SearchDefsAsync(TenantId tenantId, string? keyword, string? shareScope, int pageIndex, int pageSize, CancellationToken cancellationToken);
    Task<long> PublishVersionAsync(TenantId tenantId, long currentUserId, long defId, PluginPublishVersionRequest request, CancellationToken cancellationToken);
    Task<long> AuthorizeAsync(TenantId tenantId, long currentUserId, string pluginId, PluginAuthorizeRequest request, CancellationToken cancellationToken);
    Task<PluginInvokeResult> InvokeAsync(TenantId tenantId, long currentUserId, PluginInvokeRequest request, CancellationToken cancellationToken);
    Task<PluginUsageDto?> GetUsageAsync(TenantId tenantId, string pluginId, string day, CancellationToken cancellationToken);
}
