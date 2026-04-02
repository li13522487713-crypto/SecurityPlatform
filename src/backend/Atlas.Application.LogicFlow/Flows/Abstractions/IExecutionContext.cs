using Atlas.Core.Tenancy;

namespace Atlas.Application.LogicFlow.Flows.Abstractions;

public interface IExecutionContext
{
    long FlowExecutionId { get; }
    long FlowDefinitionId { get; }
    TenantId TenantId { get; }
    string? CorrelationId { get; }
    IReadOnlyDictionary<string, object> Variables { get; }

    T? GetVariable<T>(string key);
    void SetVariable(string key, object value);
    IReadOnlyDictionary<string, object>? GetNodeOutput(string nodeKey);
}
