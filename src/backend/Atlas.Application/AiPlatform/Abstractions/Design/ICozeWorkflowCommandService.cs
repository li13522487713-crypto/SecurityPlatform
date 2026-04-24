using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface ICozeWorkflowCommandService
{
    Task<long> CreateAsync(TenantId tenantId, long creatorId, CozeWorkflowCreateCommand request, CancellationToken cancellationToken);

    Task<CozeWorkflowSaveDraftResult> SaveDraftAsync(TenantId tenantId, long id, CozeWorkflowSaveDraftCommand request, CancellationToken cancellationToken);

    Task UpdateMetaAsync(TenantId tenantId, long id, CozeWorkflowUpdateMetaCommand request, CancellationToken cancellationToken);

    Task PublishAsync(TenantId tenantId, long id, long userId, CozeWorkflowPublishCommand request, CancellationToken cancellationToken);

    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);

    Task<long> CopyAsync(TenantId tenantId, long creatorId, long id, CancellationToken cancellationToken);
}
