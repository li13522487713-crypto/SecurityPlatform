using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Application.Assets.Repositories;
using Atlas.Application.Visualization.Abstractions;
using Atlas.Application.Visualization.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;
using Atlas.Domain.Alert.Entities;
using Atlas.Domain.Audit.Entities;
using Atlas.Infrastructure.Services.ApprovalFlow;
using SqlSugar;
using System.Text.Json;

namespace Atlas.Infrastructure.Services.Visualization;

/// <summary>
/// 可视化中心实现（基于审批/审计/资产数据）
/// </summary>
public sealed class VisualizationQueryService : IVisualizationQueryService
{
    private static readonly TimeSpan OverdueThreshold = TimeSpan.FromHours(24);

    private readonly ITenantProvider _tenantProvider;
    private readonly IApprovalFlowRepository _flowRepository;
    private readonly IApprovalFlowCommandService _flowCommandService;
    private readonly IApprovalInstanceRepository _instanceRepository;
    private readonly IApprovalNodeExecutionRepository _nodeExecutionRepository;
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IAuditQueryService _auditQueryService;
    private readonly IAssetRepository _assetRepository;
    private readonly ISqlSugarClient _db;

    public VisualizationQueryService(
        ITenantProvider tenantProvider,
        IApprovalFlowRepository flowRepository,
        IApprovalFlowCommandService flowCommandService,
        IApprovalInstanceRepository instanceRepository,
        IApprovalNodeExecutionRepository nodeExecutionRepository,
        IApprovalTaskRepository taskRepository,
        IAuditQueryService auditQueryService,
        IAssetRepository assetRepository,
        ISqlSugarClient db)
    {
        _tenantProvider = tenantProvider;
        _flowRepository = flowRepository;
        _flowCommandService = flowCommandService;
        _instanceRepository = instanceRepository;
        _nodeExecutionRepository = nodeExecutionRepository;
        _taskRepository = taskRepository;
        _auditQueryService = auditQueryService;
        _assetRepository = assetRepository;
        _db = db;
    }

    public async Task<VisualizationOverviewResponse> GetOverviewAsync(VisualizationFilterRequest filter, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();

        var totalFlows = await _flowRepository.GetPagedAsync(tenantId, 1, 1, null, filter.FlowType, cancellationToken);
        var draftFlows = await _flowRepository.GetPagedAsync(tenantId, 1, 1, ApprovalFlowStatus.Draft, filter.FlowType, cancellationToken);
        var runningInstances = await _instanceRepository.GetPagedAsync(tenantId, 1, 1, null, ApprovalInstanceStatus.Running, cancellationToken);

        var overdueTasks = await _taskRepository.CountByStatusAsync(
            tenantId,
            ApprovalTaskStatus.Pending,
            DateTimeOffset.UtcNow.Subtract(OverdueThreshold),
            cancellationToken);

        var alertsToday = await GetAlertsTodayAsync(tenantId, cancellationToken);

        var riskHints = new List<string>();
        if (draftFlows.TotalCount > 0)
        {
            riskHints.Add("存在未发布流程定义");
        }
        if (overdueTasks > 0)
        {
            riskHints.Add("存在超时待办任务");
        }
        if (runningInstances.TotalCount == 0)
        {
            riskHints.Add("当前无运行中的流程实例");
        }

        var overview = new VisualizationOverviewResponse(
            TotalProcesses: totalFlows.TotalCount,
            RunningInstances: runningInstances.TotalCount,
            BlockedNodes: overdueTasks,
            AlertsToday: alertsToday,
            RiskHints: riskHints);

        return overview;
    }

    public async Task<PagedResult<VisualizationProcessSummary>> GetProcessesAsync(PagedRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var (items, totalCount) = await _flowRepository.GetPagedAsync(
            tenantId,
            request.PageIndex,
            request.PageSize,
            null,
            request.Keyword,
            cancellationToken);

        var summaries = items.Select(item => new VisualizationProcessSummary
        {
            Id = item.Id.ToString(),
            Name = item.Name,
            Version = item.Version,
            Status = item.Status.ToString(),
            PublishedAt = item.PublishedAt
        }).ToList();

        return new PagedResult<VisualizationProcessSummary>(
            summaries,
            totalCount,
            request.PageIndex,
            request.PageSize);
    }

    public async Task<VisualizationProcessDetail?> GetProcessAsync(string id, CancellationToken cancellationToken)
    {
        if (!long.TryParse(id, out var processId))
        {
            return null;
        }

        var tenantId = _tenantProvider.GetTenantId();
        var entity = await _flowRepository.GetByIdAsync(tenantId, processId, cancellationToken);
        if (entity == null)
        {
            return null;
        }

        return new VisualizationProcessDetail
        {
            Id = entity.Id.ToString(),
            Name = entity.Name,
            Version = entity.Version,
            Status = entity.Status.ToString(),
            PublishedAt = entity.PublishedAt,
            DefinitionJson = entity.DefinitionJson
        };
    }

    public async Task<PagedResult<VisualizationInstanceSummary>> GetInstancesAsync(
        PagedRequest request,
        long? definitionId,
        ApprovalInstanceStatus? status,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var (items, totalCount) = await _instanceRepository.GetPagedAsync(
            tenantId,
            request.PageIndex,
            request.PageSize,
            definitionId,
            status,
            cancellationToken);

        var definitionIds = items.Select(x => x.DefinitionId).Distinct().ToArray();
        var flows = await _flowRepository.QueryByIdsAsync(tenantId, definitionIds, cancellationToken);
        var flowMap = flows.ToDictionary(x => x.Id, x => x.Name);
        var nodeNameMaps = flows.ToDictionary(x => x.Id, x => BuildNodeNameMap(x.DefinitionJson));

        var now = DateTimeOffset.UtcNow;
        var summaries = items.Select(item =>
        {
            var duration = (item.EndedAt ?? now) - item.StartedAt;
            var durationMinutes = Math.Max(0, (int)Math.Ceiling(duration.TotalMinutes));
            var flowName = flowMap.TryGetValue(item.DefinitionId, out var name) ? name : $"流程 {item.DefinitionId}";
            var currentNode = item.CurrentNodeId ?? "-";
            if (item.CurrentNodeId != null && nodeNameMaps.TryGetValue(item.DefinitionId, out var nodeMap)
                && nodeMap.TryGetValue(item.CurrentNodeId, out var nodeName))
            {
                currentNode = nodeName;
            }

            return new VisualizationInstanceSummary
            {
                Id = item.Id.ToString(),
                FlowName = flowName,
                Status = item.Status.ToString(),
                CurrentNode = currentNode,
                StartedAt = item.StartedAt,
                DurationMinutes = durationMinutes
            };
        }).ToList();

        return new PagedResult<VisualizationInstanceSummary>(
            summaries,
            totalCount,
            request.PageIndex,
            request.PageSize);
    }

    public Task<VisualizationValidationResponse> ValidateAsync(ValidateVisualizationRequest request, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.DefinitionJson))
        {
            errors.Add("定义内容为空");
            return Task.FromResult(new VisualizationValidationResponse(false, errors));
        }

        try
        {
            if (TryParseFlowDefinition(request.DefinitionJson, out var flowDefinition, out var parseError))
            {
                errors.Add(parseError ?? "流程解析失败");
            }
            else
            {
                if (flowDefinition.Nodes.Count == 0)
                {
                    errors.Add("画布为空");
                }

                var startCount = flowDefinition.Nodes.Count(n => string.Equals(n.Type, "start", StringComparison.OrdinalIgnoreCase));
                var endCount = flowDefinition.Nodes.Count(n => string.Equals(n.Type, "end", StringComparison.OrdinalIgnoreCase));
                if (startCount != 1) errors.Add("必须且只能有一个开始节点");
                if (endCount < 1) errors.Add("至少需要一个结束节点");
                if (flowDefinition.Edges.Count == 0) errors.Add("至少需要一条连线");

                var nodeIds = flowDefinition.Nodes.Select(n => n.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var edge in flowDefinition.Edges)
                {
                    if (string.IsNullOrWhiteSpace(edge.Source) || string.IsNullOrWhiteSpace(edge.Target))
                    {
                        errors.Add("存在缺失端点的连线");
                        break;
                    }
                    if (!nodeIds.Contains(edge.Source) || !nodeIds.Contains(edge.Target))
                    {
                        errors.Add("连线引用了不存在的节点");
                        break;
                    }
                }

                var graph = nodeIds.ToDictionary(id => id, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
                foreach (var edge in flowDefinition.Edges)
                {
                    if (graph.TryGetValue(edge.Source, out var list))
                    {
                        list.Add(edge.Target);
                    }
                }

                var startId = flowDefinition.Nodes.FirstOrDefault(n => string.Equals(n.Type, "start", StringComparison.OrdinalIgnoreCase))?.Id;
                if (!string.IsNullOrWhiteSpace(startId))
                {
                    var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    void Dfs(string id)
                    {
                        if (!visited.Add(id)) return;
                        foreach (var nxt in graph[id]) Dfs(nxt);
                    }
                    Dfs(startId);
                    if (visited.Count < nodeIds.Count)
                    {
                        errors.Add("存在未连通节点，请检查连线");
                    }
                }

                var conditionNodes = flowDefinition.Nodes.Where(n => string.Equals(n.Type, "condition", StringComparison.OrdinalIgnoreCase));
                foreach (var cn in conditionNodes)
                {
                    var outDegree = graph.TryGetValue(cn.Id, out var outs) ? outs.Count : 0;
                    if (outDegree < 2)
                    {
                        errors.Add($"条件节点 {cn.Label ?? cn.Id} 需要至少两个分支");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"解析错误: {ex.Message}");
        }

        var passed = errors.Count == 0;
        return Task.FromResult(new VisualizationValidationResponse(passed, errors));
    }

    public async Task<SaveVisualizationProcessResponse> SaveProcessAsync(SaveVisualizationProcessRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessException("VALIDATION_ERROR", "流程名称不能为空");
        }

        var tenantId = _tenantProvider.GetTenantId();
        if (string.IsNullOrWhiteSpace(request.ProcessId))
        {
            var created = await _flowCommandService.CreateAsync(
                tenantId,
                new Atlas.Application.Approval.Models.ApprovalFlowDefinitionCreateRequest
                {
                    Name = request.Name,
                    DefinitionJson = request.DefinitionJson
                },
                cancellationToken);

            return new SaveVisualizationProcessResponse(created.Id.ToString(), created.Version, created.Status.ToString());
        }

        if (!long.TryParse(request.ProcessId, out var processId))
        {
            throw new BusinessException("VALIDATION_ERROR", "流程ID格式不正确");
        }

        var updated = await _flowCommandService.UpdateAsync(
            tenantId,
            new Atlas.Application.Approval.Models.ApprovalFlowDefinitionUpdateRequest
            {
                Id = processId,
                Name = request.Name,
                DefinitionJson = request.DefinitionJson
            },
            cancellationToken);

        return new SaveVisualizationProcessResponse(updated.Id.ToString(), updated.Version, updated.Status.ToString());
    }

    public async Task<VisualizationPublishResponse> PublishAsync(PublishVisualizationRequest request, long publishedByUserId, CancellationToken cancellationToken)
    {
        if (!long.TryParse(request.ProcessId, out var processId))
        {
            throw new BusinessException("VALIDATION_ERROR", "流程ID格式不正确");
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _flowCommandService.PublishAsync(tenantId, processId, publishedByUserId, cancellationToken);
        var entity = await _flowRepository.GetByIdAsync(tenantId, processId, cancellationToken);
        var version = entity?.Version ?? request.Version;
        return new VisualizationPublishResponse(request.ProcessId, version, "Published");
    }

    public async Task<VisualizationInstanceDetail?> GetInstanceAsync(string id, CancellationToken cancellationToken)
    {
        if (!long.TryParse(id, out var instanceId))
        {
            return null;
        }

        var tenantId = _tenantProvider.GetTenantId();
        var instance = await _instanceRepository.GetByIdAsync(tenantId, instanceId, cancellationToken);
        if (instance == null)
        {
            return null;
        }

        var flow = await _flowRepository.GetByIdAsync(tenantId, instance.DefinitionId, cancellationToken);
        var flowName = flow?.Name ?? $"流程 {instance.DefinitionId}";
        var nodeNameMap = BuildNodeNameMap(flow?.DefinitionJson);
        var currentNodeName = instance.CurrentNodeId != null && nodeNameMap.TryGetValue(instance.CurrentNodeId, out var name)
            ? name
            : (instance.CurrentNodeId ?? "-");

        var executions = await _nodeExecutionRepository.GetByInstanceAsync(tenantId, instanceId, cancellationToken);
        var trace = executions
            .OrderBy(x => x.StartedAt)
            .Select(x =>
            {
                var duration = (x.CompletedAt ?? DateTimeOffset.UtcNow) - x.StartedAt;
                var durationMinutes = Math.Max(0, (int)Math.Ceiling(duration.TotalMinutes));
                var nodeName = nodeNameMap.TryGetValue(x.NodeId, out var mapName) ? mapName : x.NodeId;

                return new NodeTrace
                {
                    NodeId = x.NodeId,
                    Name = nodeName,
                    Status = x.Status.ToString(),
                    DurationMinutes = durationMinutes,
                    StartedAt = x.StartedAt,
                    EndedAt = x.CompletedAt
                };
            }).ToList();

        var riskHints = new List<string>();
        var pendingTasks = await _taskRepository.GetByInstanceAndStatusAsync(
            tenantId,
            instanceId,
            ApprovalTaskStatus.Pending,
            cancellationToken);
        var overdue = pendingTasks.Any(t => t.CreatedAt <= DateTimeOffset.UtcNow.Subtract(OverdueThreshold));
        if (overdue)
        {
            riskHints.Add("存在超时待办");
        }

        return new VisualizationInstanceDetail
        {
            Id = instance.Id.ToString(),
            FlowName = flowName,
            Status = instance.Status.ToString(),
            CurrentNode = currentNodeName,
            StartedAt = instance.StartedAt,
            FinishedAt = instance.EndedAt,
            Trace = trace,
            RiskHints = riskHints
        };
    }

    public async Task<VisualizationMetricsResponse> GetMetricsAsync(VisualizationFilterRequest filter, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var totalFlows = await _flowRepository.GetPagedAsync(tenantId, 1, 1, null, filter.FlowType, cancellationToken);
        var draftFlows = await _flowRepository.GetPagedAsync(tenantId, 1, 1, ApprovalFlowStatus.Draft, filter.FlowType, cancellationToken);
        var runningInstances = await _instanceRepository.GetPagedAsync(tenantId, 1, 1, null, ApprovalInstanceStatus.Running, cancellationToken);
        var completedInstances = await _instanceRepository.GetPagedAsync(tenantId, 1, 1, null, ApprovalInstanceStatus.Completed, cancellationToken);
        var pendingTasks = await _taskRepository.CountByStatusAsync(tenantId, ApprovalTaskStatus.Pending, null, cancellationToken);
        var overdueTasks = await _taskRepository.CountByStatusAsync(
            tenantId,
            ApprovalTaskStatus.Pending,
            DateTimeOffset.UtcNow.Subtract(OverdueThreshold),
            cancellationToken);
        var assets = await _assetRepository.QueryPageAsync(1, 1, null, cancellationToken);
        var alertsToday = await GetAlertsTodayAsync(tenantId, cancellationToken);
        var auditEventsToday = await GetAuditEventsTodayAsync(tenantId, cancellationToken);

        return new VisualizationMetricsResponse(
            totalFlows.TotalCount,
            draftFlows.TotalCount,
            runningInstances.TotalCount,
            completedInstances.TotalCount,
            pendingTasks,
            overdueTasks,
            assets.TotalCount,
            alertsToday,
            auditEventsToday);
    }

    public async Task<PagedResult<AuditListItem>> GetAuditAsync(PagedRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        return await _auditQueryService.QueryAuditsAsync(request, tenantId, cancellationToken);
    }

    private static bool TryParseFlowDefinition(string json, out FlowDefinition definition, out string? error)
    {
        try
        {
            if (json.Contains("\"cells\"", StringComparison.OrdinalIgnoreCase))
            {
                var canvas = JsonSerializer.Deserialize<CanvasDefinition>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (canvas == null || canvas.Cells.Count == 0)
                {
                    definition = new FlowDefinition(Array.Empty<FlowNode>(), Array.Empty<FlowEdge>());
                    error = "画布为空";
                    return false;
                }

                var nodes = canvas.Cells
                    .Where(c => !string.Equals(c.Shape, "edge", StringComparison.OrdinalIgnoreCase))
                    .Select(c => new FlowNode
                    {
                        Id = c.Id,
                        Type = c.Data.Type ?? "node",
                        Label = c.Data.Name ?? c.Id
                    }).ToList();

                var edges = canvas.Cells
                    .Where(c => string.Equals(c.Shape, "edge", StringComparison.OrdinalIgnoreCase))
                    .Where(c => c.Source?.Cell != null && c.Target?.Cell != null)
                    .Select(c => new FlowEdge
                    {
                        Source = c.Source!.Cell!,
                        Target = c.Target!.Cell!
                    }).ToList();

                definition = new FlowDefinition(nodes, edges);
                error = null;
                return true;
            }

            definition = FlowDefinitionParser.Parse(json);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            definition = new FlowDefinition(Array.Empty<FlowNode>(), Array.Empty<FlowEdge>());
            error = ex.Message;
            return false;
        }
    }

    private async Task<int> GetAlertsTodayAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var today = DateTimeOffset.UtcNow.Date;
        return await _db.Queryable<AlertRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.CreatedAt >= today)
            .CountAsync(cancellationToken);
    }

    private async Task<int> GetAuditEventsTodayAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var today = DateTimeOffset.UtcNow.Date;
        return await _db.Queryable<AuditRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.OccurredAt >= today)
            .CountAsync(cancellationToken);
    }

    private static Dictionary<string, string> BuildNodeNameMap(string? definitionJson)
    {
        if (string.IsNullOrWhiteSpace(definitionJson))
        {
            return new Dictionary<string, string>();
        }

        if (!TryParseFlowDefinition(definitionJson, out var definition, out _))
        {
            return new Dictionary<string, string>();
        }

        return definition.Nodes
            .Where(n => !string.IsNullOrWhiteSpace(n.Id))
            .GroupBy(n => n.Id)
            .ToDictionary(g => g.Key, g => g.First().Label ?? g.Key);
    }
}
