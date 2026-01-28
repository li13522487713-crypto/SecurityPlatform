using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// 审批人去重服务
/// </summary>
public sealed class DeduplicationService
{
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalUserQueryService _userQueryService;

    public DeduplicationService(
        IApprovalTaskRepository taskRepository,
        IApprovalUserQueryService userQueryService)
    {
        _taskRepository = taskRepository;
        _userQueryService = userQueryService;
    }

    /// <summary>
    /// 应用去重策略，过滤掉已审批的用户
    /// </summary>
    public async Task<IReadOnlyList<long>> ApplyDeduplicationAsync(
        TenantId tenantId,
        long instanceId,
        IReadOnlyList<long> candidateUserIds,
        FlowDefinition definition,
        FlowNode currentNode,
        CancellationToken cancellationToken)
    {
        if (candidateUserIds.Count == 0 || currentNode.DeduplicationType == DeduplicationType.None)
        {
            return candidateUserIds;
        }

        var resultUserIds = new List<long>(candidateUserIds);

        // 应用排除规则
        resultUserIds = await ApplyExclusionRulesAsync(
            tenantId,
            resultUserIds,
            currentNode.ExcludeUserIds,
            currentNode.ExcludeRoleCodes,
            cancellationToken);

        // 应用前向/后向去重
        if (currentNode.DeduplicationType == DeduplicationType.Forward || currentNode.DeduplicationType == DeduplicationType.Both)
        {
            var forwardExcluded = await GetForwardExcludedUserIdsAsync(
                tenantId,
                instanceId,
                definition,
                currentNode.Id,
                cancellationToken);
            resultUserIds = resultUserIds.Except(forwardExcluded).ToList();
        }

        if (currentNode.DeduplicationType == DeduplicationType.Backward || currentNode.DeduplicationType == DeduplicationType.Both)
        {
            var backwardExcluded = await GetBackwardExcludedUserIdsAsync(
                tenantId,
                instanceId,
                definition,
                currentNode.Id,
                cancellationToken);
            resultUserIds = resultUserIds.Except(backwardExcluded).ToList();
        }

        return resultUserIds;
    }

    /// <summary>
    /// 应用排除规则（排除指定的用户和角色）
    /// </summary>
    private async Task<List<long>> ApplyExclusionRulesAsync(
        TenantId tenantId,
        IReadOnlyList<long> userIds,
        string? excludeUserIds,
        string? excludeRoleCodes,
        CancellationToken cancellationToken)
    {
        var result = new List<long>(userIds);

        // 排除指定的用户ID
        if (!string.IsNullOrEmpty(excludeUserIds))
        {
            var excludeIds = ParseUserIds(excludeUserIds);
            result = result.Except(excludeIds).ToList();
        }

        // 排除指定角色的用户
        if (!string.IsNullOrEmpty(excludeRoleCodes))
        {
            var excludeRoleList = ParseRoleCodes(excludeRoleCodes);
            foreach (var roleCode in excludeRoleList)
            {
                var roleUserIds = await _userQueryService.GetUserIdsByRoleCodeAsync(tenantId, roleCode, cancellationToken);
                result = result.Except(roleUserIds).ToList();
            }
        }

        return result;
    }

    /// <summary>
    /// 获取前向去重需要排除的用户ID（已在之前节点审批过的用户）
    /// </summary>
    private async Task<IReadOnlyList<long>> GetForwardExcludedUserIdsAsync(
        TenantId tenantId,
        long instanceId,
        FlowDefinition definition,
        string currentNodeId,
        CancellationToken cancellationToken)
    {
        // 获取当前节点之前的所有节点（通过拓扑排序）
        var previousNodeIds = GetPreviousNodeIds(definition, currentNodeId);
        
        // 查询这些节点中已审批的用户
        var excludedUserIds = new HashSet<long>();
        foreach (var nodeId in previousNodeIds)
        {
            var nodeTasks = await _taskRepository.GetByInstanceAndNodeAsync(tenantId, instanceId, nodeId, cancellationToken);
            foreach (var task in nodeTasks)
            {
                // 只排除已审批或已拒绝的任务（不包括待办、取消、等待状态）
                if (task.Status == ApprovalTaskStatus.Approved || task.Status == ApprovalTaskStatus.Rejected)
                {
                    if (task.AssigneeType == AssigneeType.User && long.TryParse(task.AssigneeValue, out var userId))
                    {
                        excludedUserIds.Add(userId);
                    }
                }
            }
        }

        return excludedUserIds.ToList();
    }

    /// <summary>
    /// 获取后向去重需要排除的用户ID（已在之后节点审批过的用户）
    /// </summary>
    private async Task<IReadOnlyList<long>> GetBackwardExcludedUserIdsAsync(
        TenantId tenantId,
        long instanceId,
        FlowDefinition definition,
        string currentNodeId,
        CancellationToken cancellationToken)
    {
        // 获取当前节点之后的所有节点（通过拓扑排序）
        var nextNodeIds = GetNextNodeIds(definition, currentNodeId);
        
        // 查询这些节点中已审批的用户
        var excludedUserIds = new HashSet<long>();
        foreach (var nodeId in nextNodeIds)
        {
            var nodeTasks = await _taskRepository.GetByInstanceAndNodeAsync(tenantId, instanceId, nodeId, cancellationToken);
            foreach (var task in nodeTasks)
            {
                // 只排除已审批或已拒绝的任务
                if (task.Status == ApprovalTaskStatus.Approved || task.Status == ApprovalTaskStatus.Rejected)
                {
                    if (task.AssigneeType == AssigneeType.User && long.TryParse(task.AssigneeValue, out var userId))
                    {
                        excludedUserIds.Add(userId);
                    }
                }
            }
        }

        return excludedUserIds.ToList();
    }

    /// <summary>
    /// 获取当前节点之前的所有节点ID（递归遍历，支持并行网关）
    /// </summary>
    private IReadOnlyList<string> GetPreviousNodeIds(FlowDefinition definition, string currentNodeId)
    {
        var visited = new HashSet<string>();
        var result = new List<string>();
        CollectPreviousNodes(definition, currentNodeId, visited, result);
        return result;
    }

    /// <summary>
    /// 递归收集前置节点（支持并行网关）
    /// </summary>
    private void CollectPreviousNodes(FlowDefinition definition, string nodeId, HashSet<string> visited, List<string> result)
    {
        if (visited.Contains(nodeId))
        {
            return;
        }

        visited.Add(nodeId);
        var incomingEdges = definition.GetIncomingEdges(nodeId);
        
        foreach (var edge in incomingEdges)
        {
            var sourceNodeId = edge.Source;
            if (!visited.Contains(sourceNodeId))
            {
                result.Add(sourceNodeId);
                
                // 如果是并行网关，需要递归收集所有分支的前置节点
                CollectPreviousNodes(definition, sourceNodeId, visited, result);
            }
        }
    }

    /// <summary>
    /// 获取当前节点之后的所有节点ID（递归遍历，支持并行网关）
    /// </summary>
    private IReadOnlyList<string> GetNextNodeIds(FlowDefinition definition, string currentNodeId)
    {
        var visited = new HashSet<string>();
        var result = new List<string>();
        CollectNextNodes(definition, currentNodeId, visited, result);
        return result;
    }

    /// <summary>
    /// 递归收集后续节点（支持并行网关）
    /// </summary>
    private void CollectNextNodes(FlowDefinition definition, string nodeId, HashSet<string> visited, List<string> result)
    {
        if (visited.Contains(nodeId))
        {
            return;
        }

        visited.Add(nodeId);
        var outgoingEdges = definition.GetOutgoingEdges(nodeId);
        
        foreach (var edge in outgoingEdges)
        {
            var targetNodeId = edge.Target;
            if (!visited.Contains(targetNodeId))
            {
                result.Add(targetNodeId);
                
                // 如果是并行网关，需要递归收集所有分支的后续节点
                CollectNextNodes(definition, targetNodeId, visited, result);
            }
        }
    }

    /// <summary>
    /// 解析用户ID列表（支持逗号分隔或JSON数组）
    /// </summary>
    private static List<long> ParseUserIds(string value)
    {
        var userIds = new List<long>();
        if (string.IsNullOrEmpty(value))
        {
            return userIds;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(value);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var element in doc.RootElement.EnumerateArray())
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
        }
        catch
        {
            // 不是JSON数组，尝试逗号分隔
            var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                if (long.TryParse(part, out var userId))
                {
                    userIds.Add(userId);
                }
            }
        }

        return userIds;
    }

    /// <summary>
    /// 解析角色代码列表（支持逗号分隔或JSON数组）
    /// </summary>
    private static List<string> ParseRoleCodes(string value)
    {
        var roleCodes = new List<string>();
        if (string.IsNullOrEmpty(value))
        {
            return roleCodes;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(value);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (element.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var roleCode = element.GetString();
                        if (!string.IsNullOrEmpty(roleCode))
                        {
                            roleCodes.Add(roleCode);
                        }
                    }
                }
            }
        }
        catch
        {
            // 不是JSON数组，尝试逗号分隔
            var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            roleCodes.AddRange(parts);
        }

        return roleCodes;
    }
}
