using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Models;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 部门负责人管理服务实现
/// </summary>
public sealed class ApprovalDepartmentLeaderService : IApprovalDepartmentLeaderService
{
    private readonly IApprovalDepartmentLeaderRepository _leaderRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public ApprovalDepartmentLeaderService(
        IApprovalDepartmentLeaderRepository leaderRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _leaderRepository = leaderRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task SetLeaderAsync(
        TenantId tenantId,
        ApprovalDepartmentLeaderRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await _leaderRepository.GetByDepartmentIdAsync(
            tenantId,
            request.DepartmentId,
            cancellationToken);

        if (existing != null)
        {
            existing.UpdateLeader(request.LeaderUserId);
            await _leaderRepository.UpdateAsync(existing, cancellationToken);
        }
        else
        {
            var newLeader = new ApprovalDepartmentLeader(
                tenantId,
                request.DepartmentId,
                request.LeaderUserId,
                _idGeneratorAccessor.NextId());
            await _leaderRepository.AddAsync(newLeader, cancellationToken);
        }
    }

    public async Task<long?> GetLeaderUserIdAsync(
        TenantId tenantId,
        long departmentId,
        CancellationToken cancellationToken)
    {
        return await _leaderRepository.GetLeaderUserIdAsync(tenantId, departmentId, cancellationToken);
    }

    public async Task RemoveLeaderAsync(
        TenantId tenantId,
        long departmentId,
        CancellationToken cancellationToken)
    {
        await _leaderRepository.DeleteByDepartmentIdAsync(tenantId, departmentId, cancellationToken);
    }
}




