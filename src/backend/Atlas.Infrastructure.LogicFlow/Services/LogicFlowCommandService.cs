using Atlas.Application.LogicFlow.Flows.Abstractions;
using Atlas.Application.LogicFlow.Flows.Models;
using Atlas.Application.LogicFlow.Flows.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Flows;

namespace Atlas.Infrastructure.LogicFlow.Services;

public sealed class LogicFlowCommandService : ILogicFlowCommandService
{
    private readonly ILogicFlowRepository _flowRepository;
    private readonly IFlowNodeBindingRepository _nodeRepository;
    private readonly IFlowEdgeRepository _edgeRepository;
    private readonly IIdGeneratorAccessor _idGenerator;

    public LogicFlowCommandService(
        ILogicFlowRepository flowRepository,
        IFlowNodeBindingRepository nodeRepository,
        IFlowEdgeRepository edgeRepository,
        IIdGeneratorAccessor idGenerator)
    {
        _flowRepository = flowRepository;
        _nodeRepository = nodeRepository;
        _edgeRepository = edgeRepository;
        _idGenerator = idGenerator;
    }

    public async Task<long> CreateAsync(
        LogicFlowCreateRequest request,
        IReadOnlyList<FlowNodeBindingRequest> nodes,
        IReadOnlyList<FlowEdgeRequest> edges,
        TenantId tenantId,
        string userId,
        CancellationToken cancellationToken)
    {
        if (await _flowRepository.ExistsByNameAsync(request.Name, cancellationToken))
            throw new BusinessException("LOGIC_FLOW_EXISTS", $"逻辑流名称 '{request.Name}' 已存在");

        var flowId = _idGenerator.NextId();
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Name : request.DisplayName!;
        var triggerType = (FlowTriggerType)request.TriggerType;

        var flow = new LogicFlowDefinition(tenantId, request.Name, displayName, triggerType)
        {
            Id = flowId,
            Description = request.Description,
            Version = string.IsNullOrWhiteSpace(request.Version) ? "1.0.0" : request.Version!,
            TriggerConfigJson = string.IsNullOrWhiteSpace(request.TriggerConfigJson) ? "{}" : request.TriggerConfigJson!,
            InputSchemaJson = string.IsNullOrWhiteSpace(request.InputSchemaJson) ? "{}" : request.InputSchemaJson!,
            OutputSchemaJson = string.IsNullOrWhiteSpace(request.OutputSchemaJson) ? "{}" : request.OutputSchemaJson!,
            MaxRetries = request.MaxRetries ?? 3,
            TimeoutSeconds = request.TimeoutSeconds ?? 300,
            CreatedBy = userId,
        };

        await _flowRepository.AddAsync(flow, cancellationToken);

        var nodeEntities = new List<FlowNodeBinding>(nodes.Count);
        foreach (var n in nodes)
        {
            var nodeId = _idGenerator.NextId();
            var binding = new FlowNodeBinding(
                tenantId,
                flowId,
                n.NodeTypeKey,
                n.NodeInstanceKey,
                string.IsNullOrWhiteSpace(n.DisplayName) ? n.NodeInstanceKey : n.DisplayName!,
                n.PositionX,
                n.PositionY,
                n.SortOrder ?? 0)
            {
                Id = nodeId,
                ConfigJson = string.IsNullOrWhiteSpace(n.ConfigJson) ? "{}" : n.ConfigJson!,
            };
            nodeEntities.Add(binding);
        }

        await _nodeRepository.BulkInsertAsync(nodeEntities, cancellationToken);

        var edgeEntities = new List<FlowEdgeDefinition>(edges.Count);
        foreach (var e in edges)
        {
            var edgeId = _idGenerator.NextId();
            var edge = new FlowEdgeDefinition(
                tenantId,
                flowId,
                e.SourceNodeKey,
                e.SourcePortKey,
                e.TargetNodeKey,
                e.TargetPortKey,
                e.Priority ?? 0)
            {
                Id = edgeId,
                ConditionExpression = e.ConditionExpression,
                Label = e.Label,
                EdgeStyle = e.EdgeStyle,
            };
            edgeEntities.Add(edge);
        }

        await _edgeRepository.BulkInsertAsync(edgeEntities, cancellationToken);

        return flowId;
    }

    public async Task UpdateAsync(
        long id,
        LogicFlowUpdateRequest request,
        IReadOnlyList<FlowNodeBindingRequest> nodes,
        IReadOnlyList<FlowEdgeRequest> edges,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var flow = await _flowRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "逻辑流不存在");

        if (flow.TenantIdValue != tenantId.Value)
            throw new BusinessException("NOT_FOUND", "逻辑流不存在");

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Name : request.DisplayName!;
        var triggerType = (FlowTriggerType)request.TriggerType;

        flow.UpdateDefinition(
            request.Name,
            displayName,
            request.Description,
            string.IsNullOrWhiteSpace(request.Version) ? "1.0.0" : request.Version!,
            triggerType,
            string.IsNullOrWhiteSpace(request.TriggerConfigJson) ? "{}" : request.TriggerConfigJson!,
            string.IsNullOrWhiteSpace(request.InputSchemaJson) ? "{}" : request.InputSchemaJson!,
            string.IsNullOrWhiteSpace(request.OutputSchemaJson) ? "{}" : request.OutputSchemaJson!,
            request.MaxRetries ?? 3,
            request.TimeoutSeconds ?? 300,
            request.IsEnabled,
            flow.SnapshotId,
            flow.UpdatedBy);

        await _flowRepository.UpdateAsync(flow, cancellationToken);

        await _edgeRepository.DeleteByFlowIdAsync(id, cancellationToken);
        await _nodeRepository.DeleteByFlowIdAsync(id, cancellationToken);

        var nodeEntities = new List<FlowNodeBinding>(nodes.Count);
        foreach (var n in nodes)
        {
            var nodeId = _idGenerator.NextId();
            var binding = new FlowNodeBinding(
                tenantId,
                id,
                n.NodeTypeKey,
                n.NodeInstanceKey,
                string.IsNullOrWhiteSpace(n.DisplayName) ? n.NodeInstanceKey : n.DisplayName!,
                n.PositionX,
                n.PositionY,
                n.SortOrder ?? 0)
            {
                Id = nodeId,
                ConfigJson = string.IsNullOrWhiteSpace(n.ConfigJson) ? "{}" : n.ConfigJson!,
            };
            nodeEntities.Add(binding);
        }

        await _nodeRepository.BulkInsertAsync(nodeEntities, cancellationToken);

        var edgeEntities = new List<FlowEdgeDefinition>(edges.Count);
        foreach (var e in edges)
        {
            var edgeId = _idGenerator.NextId();
            var edge = new FlowEdgeDefinition(
                tenantId,
                id,
                e.SourceNodeKey,
                e.SourcePortKey,
                e.TargetNodeKey,
                e.TargetPortKey,
                e.Priority ?? 0)
            {
                Id = edgeId,
                ConditionExpression = e.ConditionExpression,
                Label = e.Label,
                EdgeStyle = e.EdgeStyle,
            };
            edgeEntities.Add(edge);
        }

        await _edgeRepository.BulkInsertAsync(edgeEntities, cancellationToken);
    }

    public async Task PublishAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var flow = await _flowRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "逻辑流不存在");

        if (flow.TenantIdValue != tenantId.Value)
            throw new BusinessException("NOT_FOUND", "逻辑流不存在");

        flow.Publish();
        await _flowRepository.UpdateAsync(flow, cancellationToken);
    }

    public async Task ArchiveAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var flow = await _flowRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "逻辑流不存在");

        if (flow.TenantIdValue != tenantId.Value)
            throw new BusinessException("NOT_FOUND", "逻辑流不存在");

        flow.Archive();
        await _flowRepository.UpdateAsync(flow, cancellationToken);
    }

    public async Task DeleteAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var flow = await _flowRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "逻辑流不存在");

        if (flow.TenantIdValue != tenantId.Value)
            throw new BusinessException("NOT_FOUND", "逻辑流不存在");

        await _edgeRepository.DeleteByFlowIdAsync(id, cancellationToken);
        await _nodeRepository.DeleteByFlowIdAsync(id, cancellationToken);

        if (!await _flowRepository.DeleteAsync(id, cancellationToken))
            throw new BusinessException("NOT_FOUND", "逻辑流不存在");
    }
}
