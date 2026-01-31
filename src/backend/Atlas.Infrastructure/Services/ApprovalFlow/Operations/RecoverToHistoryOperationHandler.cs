using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow.Operations;

/// <summary>
/// 恢复已结束流程操作处理器（将已结束的流程恢复到运行状态）
/// </summary>
public sealed class RecoverToHistoryOperationHandler : IApprovalOperationHandler
{
    private readonly IApprovalInstanceRepository _instanceRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.RecoverToHistory;

    public RecoverToHistoryOperationHandler(
        IApprovalInstanceRepository instanceRepository,
        IApprovalHistoryRepository historyRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _instanceRepository = instanceRepository;
        _historyRepository = historyRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task ExecuteAsync(
        TenantId tenantId,
        long instanceId,
        long? taskId,
        long operatorUserId,
        ApprovalOperationRequest request,
        CancellationToken cancellationToken)
    {
        var instance = await _instanceRepository.GetByIdAsync(tenantId, instanceId, cancellationToken);
        if (instance == null)
        {
            throw new BusinessException("INSTANCE_NOT_FOUND", "流程实例不存在");
        }

        // 只能恢复已结束的流程（已完成、已拒绝、已取消）
        if (instance.Status != ApprovalInstanceStatus.Completed &&
            instance.Status != ApprovalInstanceStatus.Rejected &&
            instance.Status != ApprovalInstanceStatus.Canceled)
        {
            throw new BusinessException("INSTANCE_NOT_ENDED", "只能恢复已结束的流程");
        }

        // 恢复流程：将状态改回运行状态
        instance.Recover(DateTimeOffset.UtcNow);
        await _instanceRepository.UpdateAsync(instance, cancellationToken);
    }
}





