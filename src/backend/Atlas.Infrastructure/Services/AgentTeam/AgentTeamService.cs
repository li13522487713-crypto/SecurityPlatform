using System.Text.Json;
using Atlas.Application.AgentTeam.Abstractions;
using Atlas.Application.AgentTeam.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AgentTeam.Entities;

namespace Atlas.Infrastructure.Services.AgentTeam;

public sealed class AgentTeamService : IAgentTeamQueryService, IAgentTeamCommandService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAgentTeamRepository _teamRepository;
    private readonly ISubAgentRepository _subAgentRepository;
    private readonly IOrchestrationNodeRepository _nodeRepository;
    private readonly ITeamVersionRepository _versionRepository;
    private readonly IExecutionRunRepository _runRepository;
    private readonly INodeRunRepository _nodeRunRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public AgentTeamService(
        IAgentTeamRepository teamRepository,
        ISubAgentRepository subAgentRepository,
        IOrchestrationNodeRepository nodeRepository,
        ITeamVersionRepository versionRepository,
        IExecutionRunRepository runRepository,
        INodeRunRepository nodeRunRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _teamRepository = teamRepository;
        _subAgentRepository = subAgentRepository;
        _nodeRepository = nodeRepository;
        _versionRepository = versionRepository;
        _runRepository = runRepository;
        _nodeRunRepository = nodeRunRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task<PagedResult<AgentTeamListItem>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        TeamStatus? status,
        TeamPublishStatus? publishStatus,
        TeamRiskLevel? riskLevel,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _teamRepository.GetPagedAsync(
            tenantId, keyword, status, publishStatus, riskLevel, pageIndex, pageSize, cancellationToken);

        var list = items.Select(x => new AgentTeamListItem(
            x.Id,
            x.TeamName,
            x.Description,
            x.Owner,
            x.Status,
            x.PublishStatus,
            x.PublishedVersionId,
            x.RiskLevel,
            x.Version,
            x.UpdatedAt)).ToList();

        return new PagedResult<AgentTeamListItem>(list, total, pageIndex, pageSize);
    }

    public async Task<AgentTeamDetail?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _teamRepository.FindByIdAsync(tenantId, id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        return new AgentTeamDetail(
            entity.Id,
            entity.TeamName,
            entity.Description,
            entity.Owner,
            DeserializeStringList(entity.CollaboratorsJson),
            entity.Status,
            entity.PublishStatus,
            entity.PublishedVersionId,
            entity.RiskLevel,
            entity.Version,
            DeserializeStringList(entity.TagsJson),
            entity.DefaultModelPolicyJson,
            entity.BudgetPolicyJson,
            entity.PermissionScopeJson,
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    public async Task<IReadOnlyList<SubAgentItem>> GetSubAgentsAsync(TenantId tenantId, long teamId, CancellationToken cancellationToken)
    {
        var items = await _subAgentRepository.GetByTeamIdAsync(tenantId, teamId, cancellationToken);
        return items.Select(x => new SubAgentItem(x.Id, x.TeamId, x.AgentName, x.Role, x.Goal, x.Status, x.UpdatedAt)).ToList();
    }

    public async Task<IReadOnlyList<OrchestrationNodeItem>> GetNodesAsync(TenantId tenantId, long teamId, CancellationToken cancellationToken)
    {
        var items = await _nodeRepository.GetByTeamIdAsync(tenantId, teamId, cancellationToken);
        return items.Select(x => new OrchestrationNodeItem(
            x.Id,
            x.TeamId,
            x.NodeName,
            x.NodeType,
            x.BindAgentId,
            DeserializeLongList(x.DependenciesJson),
            x.ExecutionMode,
            x.HumanApprovalRequired,
            x.IsCritical,
            x.SkipAllowed,
            x.UpdatedAt)).ToList();
    }

    public async Task<IReadOnlyList<TeamVersionItem>> GetVersionsAsync(TenantId tenantId, long teamId, CancellationToken cancellationToken)
    {
        var versions = await _versionRepository.GetByTeamIdAsync(tenantId, teamId, cancellationToken);
        return versions.Select(v => new TeamVersionItem(
            v.Id,
            v.TeamId,
            v.VersionNo,
            v.PublishStatus,
            v.PublishedBy,
            v.PublishedAt,
            v.RollbackFromVersionId)).ToList();
    }

    public async Task<AgentTeamRunDetail?> GetRunAsync(TenantId tenantId, long runId, CancellationToken cancellationToken)
    {
        var run = await _runRepository.FindByIdAsync(tenantId, runId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        return new AgentTeamRunDetail(
            run.Id,
            run.TeamId,
            run.TeamVersionId,
            run.CurrentState,
            run.InputPayloadJson,
            run.OutputResultJson,
            run.OutputSummary,
            run.ErrorRecordsJson,
            run.StartedAt,
            run.EndedAt);
    }

    public async Task<IReadOnlyList<NodeRunItem>> GetRunNodesAsync(TenantId tenantId, long runId, CancellationToken cancellationToken)
    {
        var nodeRuns = await _nodeRunRepository.GetByRunIdAsync(tenantId, runId, cancellationToken);
        return nodeRuns.Select(n => new NodeRunItem(
            n.Id,
            n.RunId,
            n.NodeId,
            n.AgentId,
            n.State,
            n.RetryCount,
            n.InputSnapshotJson,
            n.OutputSnapshotJson,
            n.ErrorCode,
            n.ErrorMessage,
            n.StartedAt,
            n.EndedAt,
            n.HumanInterventionAllowed)).ToList();
    }

    public async Task<long> CreateAsync(TenantId tenantId, AgentTeamCreateRequest request, CancellationToken cancellationToken)
    {
        var entity = new AgentTeamDefinition(
            tenantId,
            request.TeamName.Trim(),
            request.Description?.Trim(),
            request.Owner.Trim(),
            Serialize(request.Collaborators),
            request.DefaultModelPolicyJson ?? "{}",
            request.BudgetPolicyJson ?? "{}",
            request.PermissionScopeJson ?? "{}",
            request.RiskLevel,
            Serialize(request.Tags),
            _idGeneratorAccessor.NextId());
        await _teamRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(TenantId tenantId, long id, AgentTeamUpdateRequest request, CancellationToken cancellationToken)
    {
        var entity = await RequireTeamAsync(tenantId, id, cancellationToken);
        EnsureEditable(entity);
        entity.Update(
            request.TeamName.Trim(),
            request.Description?.Trim(),
            request.Owner.Trim(),
            Serialize(request.Collaborators),
            request.DefaultModelPolicyJson ?? "{}",
            request.BudgetPolicyJson ?? "{}",
            request.PermissionScopeJson ?? "{}",
            request.RiskLevel,
            Serialize(request.Tags));
        await _teamRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await RequireTeamAsync(tenantId, id, cancellationToken);
        if (entity.Status != TeamStatus.Draft)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "仅 Draft 团队允许删除。");
        }

        await _teamRepository.DeleteAsync(tenantId, id, cancellationToken);
    }

    public async Task<long> DuplicateAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var source = await RequireTeamAsync(tenantId, id, cancellationToken);
        var duplicate = new AgentTeamDefinition(
            tenantId,
            $"{source.TeamName}-Copy",
            source.Description,
            source.Owner,
            source.CollaboratorsJson,
            source.DefaultModelPolicyJson,
            source.BudgetPolicyJson,
            source.PermissionScopeJson,
            source.RiskLevel,
            source.TagsJson,
            _idGeneratorAccessor.NextId());
        await _teamRepository.AddAsync(duplicate, cancellationToken);

        var subAgents = await _subAgentRepository.GetByTeamIdAsync(tenantId, id, cancellationToken);
        var idMap = new Dictionary<long, long>();
        foreach (var sa in subAgents)
        {
            var target = new SubAgentDefinition(
                tenantId,
                duplicate.Id,
                sa.AgentName,
                sa.Role,
                sa.Goal,
                sa.PromptTemplate,
                sa.ModelConfigJson,
                sa.InputSchemaJson,
                sa.OutputSchemaJson,
                sa.TimeoutPolicyJson,
                _idGeneratorAccessor.NextId());
            target.Update(
                sa.AgentName,
                sa.Role,
                sa.Goal,
                sa.Boundaries,
                sa.PromptTemplate,
                sa.ModelConfigJson,
                sa.ToolPermissionsJson,
                sa.KnowledgeScopesJson,
                sa.InputSchemaJson,
                sa.OutputSchemaJson,
                sa.MemoryPolicyJson,
                sa.TimeoutPolicyJson,
                sa.RetryPolicyJson,
                sa.FallbackPolicyJson,
                sa.VisibilityPolicyJson,
                sa.Status);
            await _subAgentRepository.AddAsync(target, cancellationToken);
            idMap[sa.Id] = target.Id;
        }

        var nodes = await _nodeRepository.GetByTeamIdAsync(tenantId, id, cancellationToken);
        foreach (var node in nodes)
        {
            long? bindAgentId = node.BindAgentId.HasValue && idMap.TryGetValue(node.BindAgentId.Value, out var mappedAgentId)
                ? mappedAgentId
                : null;
            var target = new OrchestrationNodeDefinition(
                tenantId,
                duplicate.Id,
                node.NodeName,
                node.NodeType,
                bindAgentId,
                node.DependenciesJson,
                node.ExecutionMode,
                node.ConditionExpression,
                node.InputBindingJson,
                node.OutputBindingJson,
                node.RetryRuleJson,
                node.TimeoutRuleJson,
                node.HumanApprovalRequired,
                node.FallbackNodeId,
                node.Priority,
                node.IsCritical,
                node.SkipAllowed,
                _idGeneratorAccessor.NextId());
            await _nodeRepository.AddAsync(target, cancellationToken);
        }

        return duplicate.Id;
    }

    public async Task DisableAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await RequireTeamAsync(tenantId, id, cancellationToken);
        entity.Disable();
        await _teamRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task EnableAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await RequireTeamAsync(tenantId, id, cancellationToken);
        entity.Enable();
        await _teamRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task<long> PublishAsync(TenantId tenantId, long id, string publisher, TeamPublishRequest request, CancellationToken cancellationToken)
    {
        var team = await RequireTeamAsync(tenantId, id, cancellationToken);
        await ValidateOrchestrationAsync(tenantId, id, cancellationToken);
        team.MarkReady();
        await _teamRepository.UpdateAsync(team, cancellationToken);

        var snapshot = await BuildTeamSnapshotAsync(tenantId, id, cancellationToken);
        var versions = await _versionRepository.GetByTeamIdAsync(tenantId, id, cancellationToken);
        var nextNo = versions.Count + 1;

        var version = new TeamVersion(
            tenantId,
            id,
            $"v{nextNo}",
            team.Version,
            snapshot,
            publisher,
            request.RequiresApproval ? publisher : null,
            request.ApprovalRecordId,
            null,
            _idGeneratorAccessor.NextId());
        await _versionRepository.AddAsync(version, cancellationToken);

        team.MarkPublished(version.Id);
        await _teamRepository.UpdateAsync(team, cancellationToken);
        return version.Id;
    }

    public async Task RollbackAsync(TenantId tenantId, long id, long versionId, string publisher, CancellationToken cancellationToken)
    {
        var team = await RequireTeamAsync(tenantId, id, cancellationToken);
        var version = await _versionRepository.FindByIdAsync(tenantId, versionId, cancellationToken);
        if (version is null || version.TeamId != id)
        {
            throw new BusinessException(ErrorCodes.NotFound, "未找到指定版本。");
        }

        var versions = await _versionRepository.GetByTeamIdAsync(tenantId, id, cancellationToken);
        var rollbackVersion = new TeamVersion(
            tenantId,
            id,
            $"v{versions.Count + 1}",
            team.Version,
            version.DefinitionSnapshotJson,
            publisher,
            publisher,
            null,
            versionId,
            _idGeneratorAccessor.NextId());
        await _versionRepository.AddAsync(rollbackVersion, cancellationToken);

        team.MarkPublished(rollbackVersion.Id);
        await _teamRepository.UpdateAsync(team, cancellationToken);
    }

    public async Task<long> CreateSubAgentAsync(TenantId tenantId, long teamId, SubAgentCreateRequest request, CancellationToken cancellationToken)
    {
        await RequireTeamAsync(tenantId, teamId, cancellationToken);
        var entity = new SubAgentDefinition(
            tenantId,
            teamId,
            request.AgentName.Trim(),
            request.Role.Trim(),
            request.Goal.Trim(),
            request.PromptTemplate,
            request.ModelConfigJson,
            request.InputSchemaJson,
            request.OutputSchemaJson,
            request.TimeoutPolicyJson,
            _idGeneratorAccessor.NextId());
        entity.Update(
            request.AgentName.Trim(),
            request.Role.Trim(),
            request.Goal.Trim(),
            request.Boundaries,
            request.PromptTemplate,
            request.ModelConfigJson,
            request.ToolPermissionsJson,
            request.KnowledgeScopesJson,
            request.InputSchemaJson,
            request.OutputSchemaJson,
            request.MemoryPolicyJson,
            request.TimeoutPolicyJson,
            request.RetryPolicyJson,
            request.FallbackPolicyJson,
            request.VisibilityPolicyJson,
            request.Status);
        await _subAgentRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateSubAgentAsync(TenantId tenantId, long teamId, long subAgentId, SubAgentUpdateRequest request, CancellationToken cancellationToken)
    {
        await RequireTeamAsync(tenantId, teamId, cancellationToken);
        var entity = await _subAgentRepository.FindByIdAsync(tenantId, subAgentId, cancellationToken);
        if (entity is null || entity.TeamId != teamId)
        {
            throw new BusinessException(ErrorCodes.NotFound, "未找到子代理。");
        }

        entity.Update(
            request.AgentName.Trim(),
            request.Role.Trim(),
            request.Goal.Trim(),
            request.Boundaries,
            request.PromptTemplate,
            request.ModelConfigJson,
            request.ToolPermissionsJson,
            request.KnowledgeScopesJson,
            request.InputSchemaJson,
            request.OutputSchemaJson,
            request.MemoryPolicyJson,
            request.TimeoutPolicyJson,
            request.RetryPolicyJson,
            request.FallbackPolicyJson,
            request.VisibilityPolicyJson,
            request.Status);
        await _subAgentRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteSubAgentAsync(TenantId tenantId, long teamId, long subAgentId, CancellationToken cancellationToken)
    {
        await RequireTeamAsync(tenantId, teamId, cancellationToken);
        await _subAgentRepository.DeleteAsync(tenantId, subAgentId, cancellationToken);
    }

    public async Task<long> CreateNodeAsync(TenantId tenantId, long teamId, OrchestrationNodeCreateRequest request, CancellationToken cancellationToken)
    {
        await RequireTeamAsync(tenantId, teamId, cancellationToken);
        var entity = new OrchestrationNodeDefinition(
            tenantId,
            teamId,
            request.NodeName.Trim(),
            request.NodeType,
            request.BindAgentId,
            Serialize(request.Dependencies),
            request.ExecutionMode,
            request.ConditionExpression,
            request.InputBindingJson,
            request.OutputBindingJson,
            request.RetryRuleJson,
            request.TimeoutRuleJson,
            request.HumanApprovalRequired,
            request.FallbackNodeId,
            request.Priority,
            request.IsCritical,
            request.SkipAllowed,
            _idGeneratorAccessor.NextId());
        await _nodeRepository.AddAsync(entity, cancellationToken);
        await ValidateOrchestrationAsync(tenantId, teamId, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateNodeAsync(TenantId tenantId, long teamId, long nodeId, OrchestrationNodeUpdateRequest request, CancellationToken cancellationToken)
    {
        await RequireTeamAsync(tenantId, teamId, cancellationToken);
        var entity = await _nodeRepository.FindByIdAsync(tenantId, nodeId, cancellationToken);
        if (entity is null || entity.TeamId != teamId)
        {
            throw new BusinessException(ErrorCodes.NotFound, "未找到编排节点。");
        }

        entity.Assign(
            teamId,
            request.NodeName.Trim(),
            request.NodeType,
            request.BindAgentId,
            Serialize(request.Dependencies),
            request.ExecutionMode,
            request.ConditionExpression,
            request.InputBindingJson,
            request.OutputBindingJson,
            request.RetryRuleJson,
            request.TimeoutRuleJson,
            request.HumanApprovalRequired,
            request.FallbackNodeId,
            request.Priority,
            request.IsCritical,
            request.SkipAllowed);
        await _nodeRepository.UpdateAsync(entity, cancellationToken);
        await ValidateOrchestrationAsync(tenantId, teamId, cancellationToken);
    }

    public async Task DeleteNodeAsync(TenantId tenantId, long teamId, long nodeId, CancellationToken cancellationToken)
    {
        await RequireTeamAsync(tenantId, teamId, cancellationToken);
        await _nodeRepository.DeleteAsync(tenantId, nodeId, cancellationToken);
    }

    public async Task ValidateOrchestrationAsync(TenantId tenantId, long teamId, CancellationToken cancellationToken)
    {
        var nodes = await _nodeRepository.GetByTeamIdAsync(tenantId, teamId, cancellationToken);
        if (nodes.Count == 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "编排至少需要一个节点。");
        }

        var nodeIds = nodes.Select(n => n.Id).ToHashSet();
        var graph = new Dictionary<long, List<long>>();
        foreach (var node in nodes)
        {
            var deps = DeserializeLongList(node.DependenciesJson);
            if (deps.Any(dep => !nodeIds.Contains(dep)))
            {
                throw new BusinessException(ErrorCodes.ValidationError, $"节点 {node.NodeName} 依赖了不存在的节点。");
            }

            graph[node.Id] = deps.ToList();
        }

        var visited = new HashSet<long>();
        var stack = new HashSet<long>();
        foreach (var id in graph.Keys)
        {
            if (HasCycle(id, graph, visited, stack))
            {
                throw new BusinessException(ErrorCodes.ValidationError, "检测到循环依赖。");
            }
        }

        var roots = graph.Where(x => x.Value.Count == 0).Select(x => x.Key).ToHashSet();
        if (roots.Count == 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "编排缺少起始节点。");
        }

        var reachable = new HashSet<long>(roots);
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var node in nodes)
            {
                var deps = DeserializeLongList(node.DependenciesJson);
                if (deps.All(reachable.Contains) && reachable.Add(node.Id))
                {
                    changed = true;
                }
            }
        }

        if (reachable.Count != nodes.Count)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "存在孤立节点或不可达节点。");
        }
    }

    public async Task<long> CreateRunAsync(TenantId tenantId, string triggerBy, AgentTeamRunCreateRequest request, CancellationToken cancellationToken)
    {
        var team = await RequireTeamAsync(tenantId, request.TeamId, cancellationToken);
        if (team.Status != TeamStatus.Published && team.Status != TeamStatus.Ready)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "团队未发布，无法运行。");
        }

        var run = new ExecutionRun(
            tenantId,
            request.TeamId,
            request.TeamVersionId,
            triggerBy,
            request.TriggerType,
            request.InputPayloadJson,
            _idGeneratorAccessor.NextId());
        await _runRepository.AddAsync(run, cancellationToken);

        run.TransitionTo(RunStatus.Planning);
        await _runRepository.UpdateAsync(run, cancellationToken);

        var nodes = await _nodeRepository.GetByTeamIdAsync(tenantId, request.TeamId, cancellationToken);
        var nodeRuns = new List<NodeRun>();
        foreach (var node in nodes)
        {
            var nodeRun = new NodeRun(
                tenantId,
                run.Id,
                node.Id,
                node.BindAgentId,
                request.InputPayloadJson,
                node.HumanApprovalRequired,
                _idGeneratorAccessor.NextId());
            if (node.HumanApprovalRequired)
            {
                nodeRun.WaitApproval();
            }
            else
            {
                nodeRun.Succeed($"{{\"nodeId\":{node.Id},\"message\":\"simulated\"}}");
            }

            nodeRuns.Add(nodeRun);
        }

        await _nodeRunRepository.AddRangeAsync(nodeRuns, cancellationToken);

        if (nodeRuns.Any(n => n.State == NodeRunStatus.WaitingApproval))
        {
            run.TransitionTo(RunStatus.WaitingHuman);
        }
        else
        {
            run.Complete("{\"result\":\"ok\"}", "模拟执行完成", "success");
        }

        await _runRepository.UpdateAsync(run, cancellationToken);
        return run.Id;
    }

    public async Task CancelRunAsync(TenantId tenantId, long runId, CancellationToken cancellationToken)
    {
        var run = await RequireRunAsync(tenantId, runId, cancellationToken);
        run.TransitionTo(RunStatus.Cancelled);
        await _runRepository.UpdateAsync(run, cancellationToken);
    }

    public async Task InterveneRunAsync(TenantId tenantId, long runId, long nodeRunId, AgentTeamRunInterveneRequest request, CancellationToken cancellationToken)
    {
        var run = await RequireRunAsync(tenantId, runId, cancellationToken);
        var nodeRun = await _nodeRunRepository.FindByIdAsync(tenantId, nodeRunId, cancellationToken);
        if (nodeRun is null || nodeRun.RunId != runId)
        {
            throw new BusinessException(ErrorCodes.NotFound, "未找到节点运行记录。");
        }

        if (request.Action == "skip")
        {
            nodeRun.Skip();
        }
        else if (request.Action == "retry")
        {
            nodeRun.Retry();
            nodeRun.Succeed(request.PayloadJson ?? "{\"message\":\"retry-success\"}");
        }
        else
        {
            nodeRun.Succeed(request.PayloadJson ?? "{\"message\":\"confirmed\"}");
        }

        await _nodeRunRepository.UpdateAsync(nodeRun, cancellationToken);

        var allNodes = await _nodeRunRepository.GetByRunIdAsync(tenantId, runId, cancellationToken);
        if (allNodes.All(n => n.State is NodeRunStatus.Succeeded or NodeRunStatus.Skipped))
        {
            run.Complete("{\"result\":\"human-intervened\"}", "人工介入后完成", "manual");
            await _runRepository.UpdateAsync(run, cancellationToken);
        }
    }

    public async Task<AgentTeamDebugResult> DebugAsync(TenantId tenantId, long teamId, AgentTeamDebugRequest request, CancellationToken cancellationToken)
    {
        await ValidateOrchestrationAsync(tenantId, teamId, cancellationToken);
        var runId = await CreateRunAsync(
            tenantId,
            "debug",
            new AgentTeamRunCreateRequest(teamId, (await RequireTeamAsync(tenantId, teamId, cancellationToken)).PublishedVersionId ?? 0, TriggerType.Manual, request.InputPayloadJson),
            cancellationToken);

        var run = await RequireRunAsync(tenantId, runId, cancellationToken);
        var nodeRuns = await GetRunNodesAsync(tenantId, runId, cancellationToken);
        return new AgentTeamDebugResult(
            run.CurrentState is RunStatus.Completed or RunStatus.WaitingHuman,
            run.CurrentState == RunStatus.WaitingHuman ? "调试已暂停，等待人工确认节点。" : "调试完成。",
            run.OutputResultJson,
            nodeRuns);
    }

    private async Task<string> BuildTeamSnapshotAsync(TenantId tenantId, long teamId, CancellationToken cancellationToken)
    {
        var team = await RequireTeamAsync(tenantId, teamId, cancellationToken);
        var subAgents = await _subAgentRepository.GetByTeamIdAsync(tenantId, teamId, cancellationToken);
        var nodes = await _nodeRepository.GetByTeamIdAsync(tenantId, teamId, cancellationToken);
        var snapshot = new
        {
            Team = new
            {
                team.Id,
                team.TeamName,
                team.Description,
                team.Owner,
                team.Status,
                team.Version,
                team.PublishStatus,
                team.RiskLevel,
                team.DefaultModelPolicyJson,
                team.BudgetPolicyJson,
                team.PermissionScopeJson
            },
            SubAgents = subAgents.Select(x => new
            {
                x.Id,
                x.TeamId,
                x.AgentName,
                x.Role,
                x.Goal,
                x.Boundaries,
                x.PromptTemplate,
                x.ModelConfigJson,
                x.ToolPermissionsJson,
                x.KnowledgeScopesJson,
                x.InputSchemaJson,
                x.OutputSchemaJson,
                x.MemoryPolicyJson,
                x.TimeoutPolicyJson,
                x.RetryPolicyJson,
                x.FallbackPolicyJson,
                x.VisibilityPolicyJson,
                x.Status
            }),
            Nodes = nodes.Select(x => new
            {
                x.Id,
                x.TeamId,
                x.NodeName,
                x.NodeType,
                x.BindAgentId,
                x.DependenciesJson,
                x.ExecutionMode,
                x.ConditionExpression,
                x.InputBindingJson,
                x.OutputBindingJson,
                x.RetryRuleJson,
                x.TimeoutRuleJson,
                x.HumanApprovalRequired,
                x.FallbackNodeId,
                x.Priority,
                x.IsCritical,
                x.SkipAllowed
            })
        };
        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    private async Task<AgentTeamDefinition> RequireTeamAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _teamRepository.FindByIdAsync(tenantId, id, cancellationToken);
        if (entity is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "未找到 Agent 团队。");
        }

        return entity;
    }

    private async Task<ExecutionRun> RequireRunAsync(TenantId tenantId, long runId, CancellationToken cancellationToken)
    {
        var run = await _runRepository.FindByIdAsync(tenantId, runId, cancellationToken);
        if (run is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "未找到执行实例。");
        }

        return run;
    }

    private static void EnsureEditable(AgentTeamDefinition entity)
    {
        if (entity.Status == TeamStatus.Archived)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "已归档团队不可编辑。");
        }
    }

    private static IReadOnlyList<string> DeserializeStringList(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
    }

    private static IReadOnlyList<long> DeserializeLongList(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<long>();
        }

        return JsonSerializer.Deserialize<List<long>>(json, JsonOptions) ?? [];
    }

    private static string Serialize<T>(IReadOnlyList<T> values)
    {
        return JsonSerializer.Serialize(values ?? [], JsonOptions);
    }

    private static bool HasCycle(long nodeId, Dictionary<long, List<long>> graph, HashSet<long> visited, HashSet<long> stack)
    {
        if (stack.Contains(nodeId))
        {
            return true;
        }

        if (!visited.Add(nodeId))
        {
            return false;
        }

        stack.Add(nodeId);
        if (graph.TryGetValue(nodeId, out var deps))
        {
            foreach (var dep in deps)
            {
                if (HasCycle(dep, graph, visited, stack))
                {
                    return true;
                }
            }
        }

        stack.Remove(nodeId);
        return false;
    }
}
