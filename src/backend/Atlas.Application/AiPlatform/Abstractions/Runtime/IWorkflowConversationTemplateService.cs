using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions.Runtime;

public interface IWorkflowConversationTemplateService
{
    Task<IReadOnlyList<AiAppConversationTemplateListItem>> ListAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken);

    Task<AiAppConversationTemplateDetail?> GetAsync(
        TenantId tenantId,
        long appId,
        long templateId,
        CancellationToken cancellationToken);

    Task<long> CreateAsync(
        TenantId tenantId,
        long userId,
        long appId,
        AiAppConversationTemplateCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        long userId,
        long appId,
        long templateId,
        AiAppConversationTemplateUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        long userId,
        long appId,
        long templateId,
        CancellationToken cancellationToken);
}
