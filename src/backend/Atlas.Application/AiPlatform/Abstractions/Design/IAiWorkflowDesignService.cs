using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAiWorkflowDesignService
{
    Task<PagedResult<AiWorkflowDefinitionDto>> ListAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<AiWorkflowDetailDto?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<long> CreateAsync(
        TenantId tenantId,
        long creatorId,
        AiWorkflowCreateRequest request,
        CancellationToken cancellationToken);

    Task SaveAsync(
        TenantId tenantId,
        long id,
        AiWorkflowSaveRequest request,
        CancellationToken cancellationToken);

    Task UpdateMetaAsync(
        TenantId tenantId,
        long id,
        AiWorkflowMetaUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<long> CopyAsync(
        TenantId tenantId,
        long creatorId,
        long id,
        CancellationToken cancellationToken);

    Task PublishAsync(
        TenantId tenantId,
        long publisherId,
        long id,
        CancellationToken cancellationToken);

    Task<AiWorkflowValidateResult> ValidateAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AiWorkflowVersionItem>> GetVersionsAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<AiWorkflowVersionDiff?> GetVersionDiffAsync(
        TenantId tenantId,
        long id,
        int fromVersion,
        int toVersion,
        CancellationToken cancellationToken);

    Task<AiWorkflowRollbackResult> RollbackAsync(
        TenantId tenantId,
        long userId,
        long id,
        int targetVersion,
        CancellationToken cancellationToken);
}
