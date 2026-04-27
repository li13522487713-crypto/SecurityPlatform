using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Runtime;

public static class MicroflowVariableSnapshotMapper
{
    public static IReadOnlyDictionary<string, MicroflowRuntimeVariableValueDto> ToRuntimeVariableDtos(
        this MicroflowVariableStoreSnapshot snapshot)
        => snapshot.Variables.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToRuntimeVariableDto(),
            StringComparer.Ordinal);

    public static MicroflowRuntimeVariableValueDto ToRuntimeVariableDto(this MicroflowRuntimeVariableValue value)
        => new()
        {
            Name = value.Name,
            Type = MicroflowVariableStore.ToJsonElement(value.DataTypeJson),
            ValuePreview = MicroflowVariableStore.TrimPreview(value.ValuePreview, 200),
            RawValue = MicroflowVariableStore.ToJsonElement(value.RawValueJson),
            Source = value.SourceKind,
            Readonly = value.Readonly,
            ScopeKind = value.ScopeKind,
            RawValueJson = value.RawValueJson
        };
}
