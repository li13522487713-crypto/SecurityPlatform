using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow.Operations;

/// <summary>
/// 加签操作处理器
/// </summary>
public sealed class AddAssigneeOperationHandler : IApprovalOperationHandler
{
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalTaskAssigneeChangeRepository _assigneeChangeRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IApprovalDepartmentLeaderRepository _deptLeaderRepository;
    private readonly IIdGenerator _idGenerator;

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.AddAssignee;

    public AddAssigneeOperationHandler(
        IApprovalTaskRepository taskRepository,
        IApprovalTaskAssigneeChangeRepository assigneeChangeRepository,
        IApprovalHistoryRepository historyRepository,
        IApprovalDepartmentLeaderRepository deptLeaderRepository,
        IIdGenerator idGenerator)
    {
        _taskRepository = taskRepository;
        _assigneeChangeRepository = assigneeChangeRepository;
        _historyRepository = historyRepository;
        _deptLeaderRepository = deptLeaderRepository;
        _idGenerator = idGenerator;
    }

    public async Task ExecuteAsync(
        TenantId tenantId,
        long instanceId,
        long? taskId,
        long operatorUserId,
        ApprovalOperationRequest request,
        CancellationToken cancellationToken)
    {
        if (!taskId.HasValue)
        {
            throw new BusinessException("TASK_ID_REQUIRED", "加签操作需要指定任务ID");
        }

        if (request.AdditionalAssigneeValues == null || request.AdditionalAssigneeValues.Count == 0)
        {
            throw new BusinessException("ASSIGNEE_REQUIRED", "加签操作需要指定至少一个审批人");
        }

        var task = await _taskRepository.GetByIdAsync(tenantId, taskId.Value, cancellationToken);
        if (task == null)
        {
            throw new BusinessException("TASK_NOT_FOUND", "审批任务不存在");
        }

        // 获取同节点的所有任务
        var nodeTasks = await _taskRepository.GetByInstanceAndNodeAsync(tenantId, instanceId, task.NodeId, cancellationToken);
        var existingAssigneeValues = nodeTasks.Select(t => t.AssigneeValue).ToHashSet();

        // 为每个新审批人创建任务（批量操作）
        var newTasks = new List<ApprovalTask>();
        var changes = new List<ApprovalTaskAssigneeChange>();

        foreach (var assigneeValue in request.AdditionalAssigneeValues)
        {
            if (existingAssigneeValues.Contains(assigneeValue))
            {
                continue; // 跳过已存在的审批人
            }

            var newTask = new ApprovalTask(
                tenantId,
                instanceId,
                task.NodeId,
                task.Title,
                AssigneeType.User,
                assigneeValue,
                _idGenerator.NextId());
            newTasks.Add(newTask);

            // 记录加签操作
            var change = new ApprovalTaskAssigneeChange(
                tenantId,
                instanceId,
                task.NodeId,
                assigneeValue,
                AssigneeChangeType.Add,
                operatorUserId,
                _idGenerator.NextId(),
                newTask.Id,
                request.Comment);
            changes.Add(change);
        }

        // 批量添加任务和变更记录
        if (newTasks.Count > 0)
        {
            await _taskRepository.AddRangeAsync(newTasks, cancellationToken);
            await _assigneeChangeRepository.AddRangeAsync(changes, cancellationToken);
        }

        // 记录历史事件
        var addAssigneeEvent = new ApprovalHistoryEvent(
            tenantId,
            instanceId,
            ApprovalHistoryEventType.TaskCreated,
            null,
            task.NodeId,
            operatorUserId,
            _idGenerator.NextId());
        await _historyRepository.AddAsync(addAssigneeEvent, cancellationToken);
    }
}
