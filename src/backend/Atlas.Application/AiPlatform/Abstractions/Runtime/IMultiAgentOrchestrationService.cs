using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IMultiAgentOrchestrationService
{
    Task<PagedResult<MultiAgentOrchestrationListItem>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<MultiAgentOrchestrationDetail?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<long> CreateAsync(
        TenantId tenantId,
        long creatorUserId,
        MultiAgentOrchestrationCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        long id,
        MultiAgentOrchestrationUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<MultiAgentExecutionResult> RunAsync(
        TenantId tenantId,
        long userId,
        long orchestrationId,
        MultiAgentRunRequest request,
        CancellationToken cancellationToken);

    IAsyncEnumerable<MultiAgentStreamEvent> StreamRunAsync(
        TenantId tenantId,
        long userId,
        long orchestrationId,
        MultiAgentRunRequest request,
        CancellationToken cancellationToken);

    Task<MultiAgentExecutionResult?> GetExecutionAsync(
        TenantId tenantId,
        long executionId,
        CancellationToken cancellationToken);
}
