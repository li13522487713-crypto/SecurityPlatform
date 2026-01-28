using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using Atlas.Infrastructure.Services.ApprovalFlow;

namespace Atlas.Infrastructure.Services.ApprovalFlow.Operations;

/// <summary>
/// 退回任意节点操作处理器
/// </summary>
public sealed class BackToAnyNodeOperationHandler : IApprovalOperationHandler
{
    private readonly IApprovalInstanceRepository _instanceRepository;
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IApprovalFlowRepository _flowRepository;
    private readonly IApprovalNodeExecutionRepository _nodeExecutionRepository;
    private readonly IApprovalDepartmentLeaderRepository _deptLeaderRepository;
    private readonly IApprovalUserQueryService _userQueryService;
    private readonly IIdGenerator _idGenerator;

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.BackToAnyNode;

    public BackToAnyNodeOperationHandler(
        IApprovalInstanceRepository instanceRepository,
        IApprovalTaskRepository taskRepository,
        IApprovalHistoryRepository historyRepository,
        IApprovalFlowRepository flowRepository,
        IApprovalNodeExecutionRepository nodeExecutionRepository,
        IApprovalDepartmentLeaderRepository deptLeaderRepository,
        IApprovalUserQueryService userQueryService,
        IIdGenerator idGenerator)
    {
        _instanceRepository = instanceRepository;
        _taskRepository = taskRepository;
        _historyRepository = historyRepository;
        _flowRepository = flowRepository;
        _nodeExecutionRepository = nodeExecutionRepository;
        _deptLeaderRepository = deptLeaderRepository;
        _userQueryService = userQueryService;
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
        if (string.IsNullOrEmpty(request.TargetNodeId))
        {
            throw new BusinessException("TARGET_NODE_REQUIRED", "退回任意节点操作需要指定目标节点ID");
        }

        var instance = await _instanceRepository.GetByIdAsync(tenantId, instanceId, cancellationToken);
        if (instance == null || instance.Status != ApprovalInstanceStatus.Running)
        {
            throw new BusinessException("INSTANCE_NOT_RUNNING", "流程实例不在运行状态");
        }

        var flowDef = await _flowRepository.GetByIdAsync(tenantId, instance.DefinitionId, cancellationToken);
        if (flowDef == null)
        {
            throw new BusinessException("FLOW_NOT_FOUND", "流程定义不存在");
        }

        var flowDefinition = FlowDefinitionParser.Parse(flowDef.DefinitionJson);
        var targetNode = flowDefinition.GetNodeById(request.TargetNodeId);
        if (targetNode == null)
        {
            throw new BusinessException("NODE_NOT_FOUND", "目标节点不存在");
        }

        // 取消所有待审批任务
        var pendingTasks = await _taskRepository.GetByInstanceAndStatusAsync(tenantId, instanceId, ApprovalTaskStatus.Pending, cancellationToken);
        foreach (var pendingTask in pendingTasks)
        {
            pendingTask.Cancel();
            await _taskRepository.UpdateAsync(pendingTask, cancellationToken);
        }

        // 如果目标节点是审批节点，生成任务
        if (targetNode.Type == "approve")
        {
            // 生成任务
            var tasks = await ExpandTasksByAssigneeTypeAsync(
                tenantId,
                instance,
                targetNode.Id,
                targetNode.Label ?? "审批",
                targetNode.AssigneeType,
                targetNode.AssigneeValue ?? string.Empty,
                targetNode.ApprovalMode,
                targetNode.MissingAssigneeStrategy,
                cancellationToken);

            if (tasks.Count > 0)
            {
                await _taskRepository.AddRangeAsync(tasks, cancellationToken);
            }

            // 创建节点执行记录
            var execution = new ApprovalNodeExecution(
                tenantId,
                instanceId,
                targetNode.Id,
                ApprovalNodeExecutionStatus.Running,
                _idGenerator.NextId());
            await _nodeExecutionRepository.AddAsync(execution, cancellationToken);
        }

        // 更新实例当前节点
        instance.SetCurrentNode(request.TargetNodeId);
        await _instanceRepository.UpdateAsync(instance, cancellationToken);

        // 记录退回事件
        var backToNodeEvent = new ApprovalHistoryEvent(
            tenantId,
            instanceId,
            ApprovalHistoryEventType.NodeAdvanced,
            instance.CurrentNodeId,
            request.TargetNodeId,
            operatorUserId,
            _idGenerator.NextId());
        await _historyRepository.AddAsync(backToNodeEvent, cancellationToken);
    }

    private async Task<List<ApprovalTask>> ExpandTasksByAssigneeTypeAsync(
        TenantId tenantId,
        ApprovalProcessInstance instance,
        string nodeId,
        string nodeTitle,
        AssigneeType assigneeType,
        string assigneeValue,
        ApprovalMode approvalMode,
        MissingAssigneeStrategy missingAssigneeStrategy,
        CancellationToken cancellationToken)
    {
        var tasks = new List<ApprovalTask>();
        var userIds = new List<long>();

        switch (assigneeType)
        {
            case AssigneeType.User:
                var userIdStrings = ParseUserIds(assigneeValue);
                userIds = userIdStrings.Select(x => long.TryParse(x, out var id) ? id : (long?)null)
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .ToList();
                break;

            case AssigneeType.Role:
                if (!string.IsNullOrEmpty(assigneeValue))
                {
                    var roleUserIds = await _userQueryService.GetUserIdsByRoleCodeAsync(tenantId, assigneeValue, cancellationToken);
                    userIds.AddRange(roleUserIds);
                }
                break;

            case AssigneeType.DepartmentLeader:
                if (long.TryParse(assigneeValue, out var deptId))
                {
                    var leaderId = await _deptLeaderRepository.GetLeaderUserIdAsync(tenantId, deptId, cancellationToken);
                    if (leaderId.HasValue)
                    {
                        userIds.Add(leaderId.Value);
                    }
                }
                break;

            case AssigneeType.Loop:
                var loopUserIds = await _userQueryService.GetLoopApproversAsync(tenantId, instance.InitiatorUserId, cancellationToken: cancellationToken);
                userIds.AddRange(loopUserIds);
                break;

            case AssigneeType.Level:
                if (int.TryParse(assigneeValue, out var targetLevel) && targetLevel > 0)
                {
                    var levelUserId = await _userQueryService.GetLevelApproverAsync(tenantId, instance.InitiatorUserId, targetLevel, cancellationToken);
                    if (levelUserId.HasValue)
                    {
                        userIds.Add(levelUserId.Value);
                    }
                }
                break;

            case AssigneeType.DirectLeader:
                var directLeaderId = await _userQueryService.GetDirectLeaderUserIdAsync(tenantId, instance.InitiatorUserId, cancellationToken);
                if (directLeaderId.HasValue)
                {
                    userIds.Add(directLeaderId.Value);
                }
                break;

            case AssigneeType.StartUser:
                userIds.Add(instance.InitiatorUserId);
                break;

            case AssigneeType.Hrbp:
                var hrbpUserId = await _userQueryService.GetHrbpUserIdAsync(tenantId, instance.InitiatorUserId, cancellationToken);
                if (hrbpUserId.HasValue)
                {
                    userIds.Add(hrbpUserId.Value);
                }
                break;

            case AssigneeType.Customize:
            case AssigneeType.BusinessTable:
            case AssigneeType.OutSideAccess:
                userIds = ParseUserIdsFromInstanceData(instance.DataJson, assigneeValue);
                break;
        }

        // 验证用户ID有效性
        if (userIds.Count > 0)
        {
            userIds = (await _userQueryService.ValidateUserIdsAsync(tenantId, userIds, cancellationToken)).ToList();
        }

        // 处理缺失审批人策略
        if (userIds.Count == 0)
        {
            switch (missingAssigneeStrategy)
            {
                case MissingAssigneeStrategy.NotAllowed:
                    throw new BusinessException("MISSING_ASSIGNEE", $"节点 {nodeId} 无法找到审批人，不允许发起流程");

                case MissingAssigneeStrategy.Skip:
                    return tasks;

                case MissingAssigneeStrategy.TransferToAdmin:
                    var adminUserIds = await _userQueryService.GetUserIdsByRoleCodeAsync(tenantId, "Admin", cancellationToken);
                    if (adminUserIds.Count > 0)
                    {
                        userIds.AddRange(adminUserIds);
                    }
                    else
                    {
                        return tasks;
                    }
                    break;
            }
        }

        // 为每个用户创建任务
        int order = 1;
        foreach (var userId in userIds.Distinct())
        {
            var initialStatus = approvalMode == ApprovalMode.Sequential && order > 1
                ? ApprovalTaskStatus.Waiting
                : ApprovalTaskStatus.Pending;

            var task = new ApprovalTask(
                tenantId,
                instance.Id,
                nodeId,
                nodeTitle,
                AssigneeType.User,
                userId.ToString(),
                _idGenerator.NextId(),
                order: order,
                initialStatus: initialStatus);

            tasks.Add(task);
            order++;
        }

        return tasks;
    }

    private static List<string> ParseUserIds(string assigneeValue)
    {
        var userIds = new List<string>();
        if (string.IsNullOrEmpty(assigneeValue))
        {
            return userIds;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(assigneeValue);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    var userId = element.ValueKind switch
                    {
                        System.Text.Json.JsonValueKind.Number => element.GetInt64().ToString(),
                        System.Text.Json.JsonValueKind.String => element.GetString(),
                        _ => null
                    };
                    if (!string.IsNullOrEmpty(userId))
                    {
                        userIds.Add(userId);
                    }
                }
            }
        }
        catch
        {
            // 不是JSON数组，尝试逗号分隔
            var parts = assigneeValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            userIds.AddRange(parts);
        }

        return userIds;
    }

    private static List<long> ParseUserIdsFromInstanceData(string? dataJson, string fieldName)
    {
        var userIds = new List<long>();
        if (string.IsNullOrEmpty(dataJson) || string.IsNullOrEmpty(fieldName))
        {
            return userIds;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(dataJson);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty(fieldName, out var fieldElement))
                {
                    if (fieldElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var element in fieldElement.EnumerateArray())
                        {
                            if (element.ValueKind == System.Text.Json.JsonValueKind.Number && element.TryGetInt64(out var userId))
                            {
                                userIds.Add(userId);
                            }
                            else if (element.ValueKind == System.Text.Json.JsonValueKind.String && long.TryParse(element.GetString(), out var userIdStr))
                            {
                                userIds.Add(userIdStr);
                            }
                        }
                    }
                    else if (fieldElement.ValueKind == System.Text.Json.JsonValueKind.Number && fieldElement.TryGetInt64(out var singleUserId))
                    {
                        userIds.Add(singleUserId);
                    }
                    else if (fieldElement.ValueKind == System.Text.Json.JsonValueKind.String && long.TryParse(fieldElement.GetString(), out var singleUserIdStr))
                    {
                        userIds.Add(singleUserIdStr);
                    }
                }
            }
        }
        catch
        {
            // 解析失败，返回空列表
        }

        return userIds;
    }
}
