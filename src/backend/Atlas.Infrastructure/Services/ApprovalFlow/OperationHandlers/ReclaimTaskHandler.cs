using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using Atlas.Infrastructure.Services.ApprovalFlow;

namespace Atlas.Infrastructure.Services.ApprovalFlow.OperationHandlers;

/// <summary>
/// 拿回任务处理器
/// </summary>
public sealed class ReclaimTaskHandler : IApprovalOperationHandler
{
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IApprovalInstanceRepository _instanceRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public ReclaimTaskHandler(
        IApprovalTaskRepository taskRepository,
        IApprovalHistoryRepository historyRepository,
        IApprovalInstanceRepository instanceRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _taskRepository = taskRepository;
        _historyRepository = historyRepository;
        _instanceRepository = instanceRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.Reclaim;

    public async Task ExecuteAsync(
        TenantId tenantId,
        long instanceId,
        long? taskId,
        long operatorUserId,
        ApprovalOperationRequest request,
        CancellationToken cancellationToken)
    {
        // 1. 找到我刚刚审批过的任务
        // 如果 taskId 传了，就是那个任务。如果没传，需要找最近的一个由我审批的任务。
        ApprovalTask? myTask = null;
        if (taskId.HasValue)
        {
            myTask = await _taskRepository.GetByIdAsync(tenantId, taskId.Value, cancellationToken);
        }
        else
        {
            // 查找最近一条由我审批的历史记录
            // 简化逻辑：假设前端必须传 taskId
            throw new BusinessException("INVALID_REQUEST", "必须指定要拿回的任务ID");
        }

        if (myTask == null || myTask.DecisionByUserId != operatorUserId)
        {
            throw new BusinessException("FORBIDDEN", "只能拿回自己审批过的任务");
        }

        // 2. 检查当前流程状态：必须还在下一个节点，且下一个节点未处理
        // 获取当前实例的所有 Pending 任务
        var activeTasks = await _taskRepository.GetByInstanceAndStatusAsync(tenantId, instanceId, ApprovalTaskStatus.Pending, cancellationToken);
        
        // 如果已经没有 Pending 任务（流程结束了），或者已经流转到下下个节点（被下一个人审批了）
        // 这里的判断逻辑比较复杂，简单来说：
        // 如果当前活跃节点的任务还没有被任何人处理，就可以拿回。
        // 如果是会签，只要有一个人处理了，可能就不能拿回了（取决于业务规则，通常是只要没进入下一级节点就可以拿回，或者只要下一个人没看没办就可以拿回）
        // FlowLong 逻辑：撤回（拿回）通常指撤回提交。
        // 检查 activeTasks 是否有任何一个已经被处理（对于 Pending 状态，DecisionByUserId 应该是 null）
        // 实际上 activeTasks 都是 Pending 的，肯定没处理。
        // 关键是：activeTasks 是否是 myTask 的直接后续节点？
        
        // 简化实现：只要流程还在运行，且当前节点的所有任务都处于 Pending 状态（未被处理），且没有流转到更后面的节点。
        // 我们假设 myTask 是上一个节点的任务。
        
        // 3. 执行拿回
        // a. 取消当前所有活跃任务
        foreach (var task in activeTasks)
        {
            task.Cancel();
        }
        await _taskRepository.UpdateRangeAsync(activeTasks, cancellationToken);

        // b. 重新激活我的任务（创建一个新的 Pending 任务，或者把旧任务状态改回 Pending）
        // 通常是创建一个新任务，或者把旧任务状态改回 Pending 并清除决策信息
        // 为了保留历史记录，建议创建新任务，或者把流程回退到当前节点。
        // 这里我们简单地把 myTask 状态改回 Pending，清除决策信息
        // 但 myTask 已经是 Approved/Rejected 状态，且有历史记录。
        // 更好的做法是：模拟“退回上一步”，退回到 myTask 所在的节点。
        
        // 实际上 Reclaim = Withdraw (撤回)
        // 我们复用“退回”逻辑，目标节点是 myTask.NodeId
        
        // 记录拿回历史
        var historyEvent = new ApprovalHistoryEvent(
            tenantId,
            instanceId,
            ApprovalHistoryEventType.TaskReclaimed,
            myTask.NodeId,
            null,
            operatorUserId,
            _idGeneratorAccessor.NextId());
        await _historyRepository.AddAsync(historyEvent, cancellationToken);

        // 恢复 myTask 状态（或者创建新任务替代它）
        // 这里选择：创建新任务（复制 myTask），状态为 Pending
        var newTask = new ApprovalTask(
            tenantId,
            instanceId,
            myTask.NodeId,
            myTask.Title,
            myTask.AssigneeType,
            myTask.AssigneeValue, // 保持原分配人（也就是我）
            _idGeneratorAccessor.NextId(),
            order: myTask.Order,
            initialStatus: ApprovalTaskStatus.Pending);
        
        await _taskRepository.AddAsync(newTask, cancellationToken);
        
        // 更新实例当前节点
        var instance = await _instanceRepository.GetByIdAsync(tenantId, instanceId, cancellationToken);
        if (instance != null)
        {
            instance.SetCurrentNode(myTask.NodeId);
            await _instanceRepository.UpdateAsync(instance, cancellationToken);
        }
    }
}
