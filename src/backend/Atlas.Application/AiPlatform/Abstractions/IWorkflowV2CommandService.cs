using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

/// <summary>
/// V2 工作流写操作服务（创建/保存草稿/更新元信息/发布/删除/复制）。
/// </summary>
public interface IWorkflowV2CommandService
{
    Task<long> CreateAsync(TenantId tenantId, long creatorId, WorkflowV2CreateRequest request, CancellationToken cancellationToken);

    Task SaveDraftAsync(TenantId tenantId, long id, WorkflowV2SaveDraftRequest request, CancellationToken cancellationToken);

    Task UpdateMetaAsync(TenantId tenantId, long id, WorkflowV2UpdateMetaRequest request, CancellationToken cancellationToken);

    Task PublishAsync(TenantId tenantId, long id, long userId, WorkflowV2PublishRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);

    Task<long> CopyAsync(TenantId tenantId, long creatorId, long id, CancellationToken cancellationToken);
}
