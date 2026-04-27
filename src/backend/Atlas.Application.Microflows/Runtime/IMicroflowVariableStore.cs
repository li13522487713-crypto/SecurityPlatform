namespace Atlas.Application.Microflows.Runtime;

public interface IMicroflowVariableStore
{
    IReadOnlyDictionary<string, MicroflowRuntimeVariableValue> CurrentVariables { get; }
    void Define(MicroflowVariableDefinition definition);
    bool Exists(string name);
    MicroflowRuntimeVariableValue Get(string name);
    bool TryGet(string name, out MicroflowRuntimeVariableValue? value);
    void Set(string name, MicroflowRuntimeVariableValue value);
    void Remove(string name);
    IDisposable PushScope(MicroflowVariableScopeFrame frame);
    MicroflowVariableStoreSnapshot CreateSnapshot(MicroflowVariableSnapshotOptions options);
    IReadOnlyList<MicroflowVariableStoreDiagnostic> Diagnostics { get; }
}
