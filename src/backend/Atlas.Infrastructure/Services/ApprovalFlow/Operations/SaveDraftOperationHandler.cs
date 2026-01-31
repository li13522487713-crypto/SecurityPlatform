using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow.Operations;

/// <summary>
/// 保存草稿操作处理器（保存流程实例的草稿状态，不推进流程）
/// </summary>
public sealed class SaveDraftOperationHandler : IApprovalOperationHandler
{
    private readonly IApprovalInstanceRepository _instanceRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.SaveDraft;

    public SaveDraftOperationHandler(
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

        // 保存草稿：更新实例的 DataJson（如果提供了变量）
        if (request.Variables != null && request.Variables.Count > 0)
        {
            // 合并变量到 DataJson（简化实现：将变量序列化为JSON）
            // 实际应用中可能需要更复杂的合并逻辑
            var variablesJson = System.Text.Json.JsonSerializer.Serialize(request.Variables);
            instance.UpdateData(variablesJson);
        }

        // 保存草稿操作通常不需要改变实例状态，只是保存数据
        await _instanceRepository.UpdateAsync(instance, cancellationToken);

        // 记录历史事件
        var saveDraftEvent = new ApprovalHistoryEvent(
            tenantId,
            instanceId,
            ApprovalHistoryEventType.InstanceStarted,
            null,
            null,
            operatorUserId,
            _idGeneratorAccessor.NextId());
        await _historyRepository.AddAsync(saveDraftEvent, cancellationToken);
    }
}





