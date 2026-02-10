using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow.OperationHandlers;

/// <summary>
/// 唤醒任务处理器（用于恢复已挂起或已结束的流程）
/// </summary>
public sealed class ResumeTaskHandler : IApprovalOperationHandler
{
    private readonly IApprovalInstanceRepository _instanceRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public ResumeTaskHandler(
        IApprovalInstanceRepository instanceRepository,
        IApprovalHistoryRepository historyRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _instanceRepository = instanceRepository;
        _historyRepository = historyRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.Resume;

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
            throw new BusinessException("INSTANCE_NOT_FOUND", "实例不存在");
        }

        // 只有挂起、已完成、已驳回、已取消、已终止的流程可以唤醒
        // 这里主要针对 Suspended 状态，或者从历史中恢复
        if (instance.Status == ApprovalInstanceStatus.Running)
        {
            throw new BusinessException("INVALID_STATUS", "流程正在运行中，无需唤醒");
        }

        // 恢复状态
        instance.Recover(DateTimeOffset.UtcNow);
        await _instanceRepository.UpdateAsync(instance, cancellationToken);

        // 记录唤醒历史
        var historyEvent = new ApprovalHistoryEvent(
            tenantId,
            instanceId,
            ApprovalHistoryEventType.TaskResumed,
            instance.CurrentNodeId,
            null,
            operatorUserId,
            _idGeneratorAccessor.NextId());
        await _historyRepository.AddAsync(historyEvent, cancellationToken);
    }
}
