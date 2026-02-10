using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow.OperationHandlers;

/// <summary>
/// 追加处理人处理器
/// </summary>
public sealed class AppendAssigneeHandler : IApprovalOperationHandler
{
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public AppendAssigneeHandler(
        IApprovalTaskRepository taskRepository,
        IApprovalHistoryRepository historyRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _taskRepository = taskRepository;
        _historyRepository = historyRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.Append;

    public async Task ExecuteAsync(
        TenantId tenantId,
        long instanceId,
        long? taskId,
        long operatorUserId,
        ApprovalOperationRequest request,
        CancellationToken cancellationToken)
    {
        if (!taskId.HasValue) throw new BusinessException("INVALID_REQUEST", "任务ID不能为空");
        if (request.AdditionalAssigneeValues == null || request.AdditionalAssigneeValues.Count == 0)
        {
            throw new BusinessException("INVALID_REQUEST", "追加人员不能为空");
        }

        var currentTask = await _taskRepository.GetByIdAsync(tenantId, taskId.Value, cancellationToken);
        if (currentTask == null) throw new BusinessException("TASK_NOT_FOUND", "任务不存在");

        // 创建新任务
        foreach (var assignee in request.AdditionalAssigneeValues)
        {
            var newTask = new ApprovalTask(
                tenantId,
                instanceId,
                currentTask.NodeId,
                currentTask.Title,
                AssigneeType.User,
                assignee,
                _idGeneratorAccessor.NextId(),
                order: currentTask.Order,
                initialStatus: ApprovalTaskStatus.Pending);
            
            await _taskRepository.AddAsync(newTask, cancellationToken);
        }

        // 记录历史
        var historyEvent = new ApprovalHistoryEvent(
            tenantId,
            instanceId,
            ApprovalHistoryEventType.AssigneeAdded,
            currentTask.NodeId,
            request.Comment,
            operatorUserId,
            _idGeneratorAccessor.NextId());
        await _historyRepository.AddAsync(historyEvent, cancellationToken);
    }
}
