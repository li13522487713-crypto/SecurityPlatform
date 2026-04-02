using System.Collections.Concurrent;
using Atlas.Application.LogicFlow.Flows.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.LogicFlow.Flows;

public sealed class ExecutionContextImpl : IExecutionContext
{
    private readonly ConcurrentDictionary<string, object> _variables = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Dictionary<string, object>> _nodeOutputs = new(StringComparer.Ordinal);

    public ExecutionContextImpl(long flowExecutionId, long flowDefinitionId, TenantId tenantId, string? correlationId)
    {
        FlowExecutionId = flowExecutionId;
        FlowDefinitionId = flowDefinitionId;
        TenantId = tenantId;
        CorrelationId = correlationId;
    }

    public long FlowExecutionId { get; }
    public long FlowDefinitionId { get; }
    public TenantId TenantId { get; }
    public string? CorrelationId { get; }
    public IReadOnlyDictionary<string, object> Variables => _variables;

    public T? GetVariable<T>(string key)
    {
        if (!_variables.TryGetValue(key, out var value))
            return default;
        if (value is T typed)
            return typed;
        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default;
        }
    }

    public void SetVariable(string key, object value) => _variables[key] = value;

    public IReadOnlyDictionary<string, object>? GetNodeOutput(string nodeKey) =>
        _nodeOutputs.TryGetValue(nodeKey, out var dict) ? dict : null;

    internal void SetNodeOutput(string nodeKey, IReadOnlyDictionary<string, string> outputData)
    {
        var asObjects = outputData.ToDictionary(static kv => kv.Key, static kv => (object)kv.Value, StringComparer.Ordinal);
        _nodeOutputs[nodeKey] = asObjects;
    }
}
