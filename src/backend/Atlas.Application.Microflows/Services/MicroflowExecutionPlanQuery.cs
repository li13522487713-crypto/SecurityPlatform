using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Services;

public sealed class MicroflowExecutionPlanQuery
{
    private readonly Dictionary<string, MicroflowExecutionNode> _nodes;
    private readonly Dictionary<string, MicroflowExecutionFlow> _flows;
    private readonly Dictionary<string, MicroflowExecutionLoopCollection> _loopCollections;
    private readonly Dictionary<string, IReadOnlyList<MicroflowExecutionFlow>> _incomingFlows;
    private readonly Dictionary<string, IReadOnlyList<MicroflowExecutionFlow>> _normalOutgoingFlows;
    private readonly Dictionary<string, IReadOnlyList<MicroflowExecutionFlow>> _decisionOutgoingFlows;
    private readonly Dictionary<string, IReadOnlyList<MicroflowExecutionFlow>> _objectTypeOutgoingFlows;
    private readonly Dictionary<string, IReadOnlyList<MicroflowExecutionFlow>> _errorHandlerOutgoingFlows;

    public MicroflowExecutionPlanQuery(MicroflowExecutionPlan plan)
    {
        _nodes = plan.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.ObjectId))
            .GroupBy(node => node.ObjectId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        _flows = plan.Flows
            .Where(flow => !string.IsNullOrWhiteSpace(flow.FlowId))
            .GroupBy(flow => flow.FlowId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        _loopCollections = plan.LoopCollections
            .Where(loop => !string.IsNullOrWhiteSpace(loop.LoopObjectId))
            .GroupBy(loop => loop.LoopObjectId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        _incomingFlows = Bucket(plan.Flows.Where(flow => !string.IsNullOrWhiteSpace(flow.DestinationObjectId)), flow => flow.DestinationObjectId!);
        _normalOutgoingFlows = Bucket(plan.NormalFlows, flow => flow.OriginObjectId);
        _decisionOutgoingFlows = Bucket(plan.DecisionFlows, flow => flow.OriginObjectId);
        _objectTypeOutgoingFlows = Bucket(plan.ObjectTypeFlows, flow => flow.OriginObjectId);
        _errorHandlerOutgoingFlows = Bucket(plan.ErrorHandlerFlows, flow => flow.OriginObjectId);
    }

    public MicroflowExecutionNode GetNode(MicroflowExecutionPlan plan, string objectId)
        => TryGetNode(plan, objectId, out var node)
            ? node
            : throw new KeyNotFoundException($"ExecutionPlan node not found: {objectId}");

    public bool TryGetNode(MicroflowExecutionPlan plan, string objectId, out MicroflowExecutionNode node)
    {
        _ = plan;
        return _nodes.TryGetValue(objectId, out node!);
    }

    public MicroflowExecutionFlow GetFlow(MicroflowExecutionPlan plan, string flowId)
        => _flows.TryGetValue(flowId, out var flow)
            ? flow
            : throw new KeyNotFoundException($"ExecutionPlan flow not found: {flowId}");

    public IReadOnlyList<MicroflowExecutionFlow> GetNormalOutgoingFlows(MicroflowExecutionPlan plan, string objectId, string? collectionId)
    {
        _ = plan;
        return FilterByCollection(_normalOutgoingFlows.GetValueOrDefault(objectId) ?? Array.Empty<MicroflowExecutionFlow>(), collectionId);
    }

    public IReadOnlyList<MicroflowExecutionFlow> GetDecisionOutgoingFlows(MicroflowExecutionPlan plan, string objectId, string? collectionId)
    {
        _ = plan;
        return FilterByCollection(_decisionOutgoingFlows.GetValueOrDefault(objectId) ?? Array.Empty<MicroflowExecutionFlow>(), collectionId);
    }

    public IReadOnlyList<MicroflowExecutionFlow> GetObjectTypeOutgoingFlows(MicroflowExecutionPlan plan, string objectId, string? collectionId)
    {
        _ = plan;
        return FilterByCollection(_objectTypeOutgoingFlows.GetValueOrDefault(objectId) ?? Array.Empty<MicroflowExecutionFlow>(), collectionId);
    }

    public IReadOnlyList<MicroflowExecutionFlow> GetErrorHandlerFlows(MicroflowExecutionPlan plan, string objectId, string? collectionId)
    {
        _ = plan;
        return FilterByCollection(_errorHandlerOutgoingFlows.GetValueOrDefault(objectId) ?? Array.Empty<MicroflowExecutionFlow>(), collectionId);
    }

    public IReadOnlyList<MicroflowExecutionFlow> GetIncomingFlows(MicroflowExecutionPlan plan, string objectId, string? collectionId)
    {
        _ = plan;
        return FilterByCollection(_incomingFlows.GetValueOrDefault(objectId) ?? Array.Empty<MicroflowExecutionFlow>(), collectionId);
    }

    public MicroflowExecutionLoopCollection? GetLoopCollection(MicroflowExecutionPlan plan, string loopObjectId)
    {
        _ = plan;
        return _loopCollections.GetValueOrDefault(loopObjectId);
    }

    public bool IsTerminalNode(MicroflowExecutionNode node)
        => IsKind(node, "endEvent") || IsKind(node, "errorEvent") || IsKind(node, "breakEvent") || IsKind(node, "continueEvent");

    public bool IsIgnoredNode(MicroflowExecutionNode node)
        => string.Equals(node.RuntimeBehavior, "ignored", StringComparison.OrdinalIgnoreCase)
            || IsKind(node, "annotation")
            || IsKind(node, "parameterObject");

    public bool IsExecutableNode(MicroflowExecutionNode node)
        => string.Equals(node.RuntimeBehavior, "executable", StringComparison.OrdinalIgnoreCase)
            || IsKind(node, "startEvent")
            || IsKind(node, "exclusiveMerge")
            || IsKind(node, "exclusiveSplit")
            || IsKind(node, "inheritanceSplit")
            || IsKind(node, "loopedActivity")
            || IsTerminalNode(node);

    public bool IsUnsupportedNode(MicroflowExecutionNode node)
        => string.Equals(node.RuntimeBehavior, "unsupported", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(node.SupportLevel, MicroflowRuntimeSupportLevel.Supported, StringComparison.OrdinalIgnoreCase);

    public bool IsFlowInCollection(MicroflowExecutionFlow flow, string? collectionId)
        => string.Equals(flow.CollectionId, collectionId, StringComparison.Ordinal);

    public MicroflowExecutionFlow? GetDefaultNormalOutgoingFlow(MicroflowExecutionPlan plan, string objectId, string? collectionId)
        => GetNormalOutgoingFlows(plan, objectId, collectionId).FirstOrDefault();

    public string? FindLoopEntryNodeId(MicroflowExecutionPlan plan, MicroflowExecutionLoopCollection loop)
    {
        foreach (var startLikeNodeId in loop.StartLikeNodeIds)
        {
            if (_nodes.ContainsKey(startLikeNodeId))
            {
                return startLikeNodeId;
            }
        }

        var candidates = loop.Nodes
            .Select(nodeId => _nodes.GetValueOrDefault(nodeId))
            .Where(node => node is not null && !IsIgnoredNode(node))
            .Cast<MicroflowExecutionNode>()
            .ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        var incomingTargets = plan.Flows
            .Where(flow => IsFlowInCollection(flow, loop.CollectionId) && !string.Equals(flow.ControlFlow, "ignored", StringComparison.OrdinalIgnoreCase))
            .Select(flow => flow.DestinationObjectId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        return candidates.FirstOrDefault(node => !incomingTargets.Contains(node.ObjectId))?.ObjectId
            ?? candidates[0].ObjectId;
    }

    private static Dictionary<string, IReadOnlyList<MicroflowExecutionFlow>> Bucket(
        IEnumerable<MicroflowExecutionFlow> flows,
        Func<MicroflowExecutionFlow, string?> keySelector)
        => flows
            .Where(flow => !string.IsNullOrWhiteSpace(keySelector(flow)))
            .GroupBy(flow => keySelector(flow)!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<MicroflowExecutionFlow>)group
                    .OrderBy(flow => flow.BranchOrder ?? int.MaxValue)
                    .ThenBy(flow => flow.OriginConnectionIndex ?? int.MaxValue)
                    .ThenBy(flow => flow.FlowId, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

    private IReadOnlyList<MicroflowExecutionFlow> FilterByCollection(
        IReadOnlyList<MicroflowExecutionFlow> flows,
        string? collectionId)
        => flows.Where(flow => IsFlowInCollection(flow, collectionId)).ToArray();

    private static bool IsKind(MicroflowExecutionNode node, string kind)
        => string.Equals(node.Kind, kind, StringComparison.OrdinalIgnoreCase);
}
