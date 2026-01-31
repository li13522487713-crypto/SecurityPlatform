using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;

namespace Atlas.Application.Approval.Repositories;

/// <summary>
/// 并行网关 Token 仓储接口
/// </summary>
public interface IApprovalParallelTokenRepository
{
    Task AddAsync(ApprovalParallelToken entity, CancellationToken cancellationToken);

    Task AddRangeAsync(IEnumerable<ApprovalParallelToken> entities, CancellationToken cancellationToken);

    Task UpdateAsync(ApprovalParallelToken entity, CancellationToken cancellationToken);

    Task UpdateRangeAsync(IEnumerable<ApprovalParallelToken> entities, CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalParallelToken>> GetByInstanceAndGatewayAsync(
        TenantId tenantId,
        long instanceId,
        string gatewayNodeId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalParallelToken>> GetByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken);
}
