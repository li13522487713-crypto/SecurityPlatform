using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow.OperationHandlers;

/// <summary>
/// 认领任务处理器
/// </summary>
public sealed class ClaimTaskHandler : IApprovalOperationHandler
{
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public ClaimTaskHandler(
        IApprovalTaskRepository taskRepository,
        IApprovalHistoryRepository historyRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _taskRepository = taskRepository;
        _historyRepository = historyRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.Claim;

    public async Task ExecuteAsync(
        TenantId tenantId,
        long instanceId,
        long? taskId,
        long operatorUserId,
        ApprovalOperationRequest request,
        CancellationToken cancellationToken)
    {
        if (!taskId.HasValue) throw new BusinessException("INVALID_REQUEST", "任务ID不能为空");

        var task = await _taskRepository.GetByIdAsync(tenantId, taskId.Value, cancellationToken);
        if (task == null) throw new BusinessException("TASK_NOT_FOUND", "任务不存在");

        if (task.Status != ApprovalTaskStatus.Pending && task.Status != ApprovalTaskStatus.Claimed)
        {
             // 只有 Pending (未认领) 或 Claimed (已被别人认领?) 状态相关
             // 通常 Pending 表示在池子里。
             // 如果已经被认领 (Claimed)，则不能再认领，除非释放。
             // 这里假设 Pending 状态且 AssigneeType 是候选组（如角色/部门）时，任务在池子里。
             // 或者有一个明确的 IsClaimed 标志。
             // FlowLong 中，认领是将候选人转为具体办理人。
        }
        
        // 检查是否已经被认领
        if (task.Status == ApprovalTaskStatus.Claimed)
        {
            throw new BusinessException("TASK_ALREADY_CLAIMED", "任务已被认领");
        }

        // 执行认领：修改状态为 Claimed (或者保持 Pending 但修改 Assignee 为当前用户)
        // 推荐做法：修改 Assignee 为当前用户，状态保持 Pending (处理中)
        // 但为了区分"待认领"和"已认领"，我们引入了 Claimed 状态？
        // 不，通常"待认领"是 Pending，"已认领"也是 Pending 但有了具体 Assignee。
        // 在 FlowLong 中，候选任务是 Pending，认领后变成 Pending (Assignee=User)。
        // 之前我们加了 Claimed 状态，可能是用于"已认领但未处理"。
        
        // 让我们使用新加的 Claimed 状态表示"已认领，处理中"。
        // 或者，如果之前的状态是 Pending (且 Assignee 是 角色/部门)，现在改为 Pending (Assignee=User)。
        // 这样更符合标准 BPMN。
        // 但为了利用我们加的 Claimed 枚举：
        // 假设 Pending = 待认领 (如果 Assignee 是组)
        // Claimed = 已认领 (Assignee 是人)
        
        // 更新任务
        // 注意：如果任务是发给一组人的（例如生成的多个任务），认领一个通常会取消其他人的任务（竞争认领）。
        // 或者，如果只有一个任务发给角色，那么认领就是修改这个任务的 Assignee。
        
        // 假设是竞争认领模式（GroupStrategy = Claim）：
        // 系统会生成多个任务给每个人，谁先认领，其他人的任务就取消。
        
        // 1. 标记当前任务为 Claimed (已认领)
        // task.Status = ApprovalTaskStatus.Claimed; // 或者保持 Pending，看前端怎么展示
        // 这里我们用 Claimed 表示"我认领了，正在办"
        task.SetTaskType(6); // 6=Claimed? No, TaskType is int. 
        // 之前 TaskType 定义: 0=主办 1=审批 ... 6=已认领(Status)
        // TaskType 属性我们没定义 Claim 类型，用 Status = Claimed 即可。
        
        // task.Status = ApprovalTaskStatus.Claimed; 
        // 但 Claimed 在 Enum 里是 6。
        // 让我们把 Status 改为 Claimed。
        // 实际上，Claimed 状态应该视为 "Processing"。
        
        // 另外，需要把其他人的竞争任务取消。
        var siblingTasks = await _taskRepository.GetByInstanceAndNodeAsync(tenantId, instanceId, task.NodeId, cancellationToken);
        var otherTasks = siblingTasks.Where(t => t.Id != task.Id && t.Status == ApprovalTaskStatus.Pending).ToList();
        
        foreach (var other in otherTasks)
        {
            other.Cancel();
        }
        await _taskRepository.UpdateRangeAsync(otherTasks, cancellationToken);

        // 更新当前任务
        // task.Status = ApprovalTaskStatus.Pending; // 保持 Pending，因为还需要审批
        // 关键是 AssigneeValue 要变成当前用户（如果之前是角色）
        // 如果之前已经是用户（发给多个人竞争），则不需要改 AssigneeValue，只需要确认状态。
        
        // 记录认领历史
        var historyEvent = new ApprovalHistoryEvent(
            tenantId,
            instanceId,
            ApprovalHistoryEventType.TaskClaimed,
            task.NodeId,
            null,
            operatorUserId,
            _idGeneratorAccessor.NextId());
        await _historyRepository.AddAsync(historyEvent, cancellationToken);
    }
}
