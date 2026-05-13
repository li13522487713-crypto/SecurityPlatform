using Atlas.Application.Microflows.Runtime;

namespace Atlas.Application.Microflows.Runtime.Branches;

public sealed record BranchExecutionContext
{
    public string BranchId { get; init; } = Guid.NewGuid().ToString("N");

    public IMicroflowVariableStore VariableStore { get; init; } = new MicroflowVariableStore();

    public IReadOnlySet<string> WrittenVariableNames { get; init; } = new HashSet<string>(StringComparer.Ordinal);
}

public sealed record BranchMergedVariable
{
    public string BranchId { get; init; } = string.Empty;
    public string VariableName { get; init; } = string.Empty;
    public MicroflowRuntimeVariableValue Value { get; init; } = new();
}

public sealed record BranchMergeResult
{
    public IReadOnlyList<BranchMergedVariable> Variables { get; init; } = Array.Empty<BranchMergedVariable>();
}

public interface IVariableScopeForker
{
    IMicroflowVariableStore Fork(IMicroflowVariableStore source, string branchId);
}

public interface IBranchMergePolicy
{
    BranchMergeResult Merge(IMicroflowVariableStore target, IReadOnlyList<BranchExecutionContext> branches);
}

public sealed class DefaultVariableScopeForker : IVariableScopeForker
{
    public IMicroflowVariableStore Fork(IMicroflowVariableStore source, string branchId)
    {
        var forked = new MicroflowVariableStore();
        foreach (var pair in source.CurrentVariables)
        {
            forked.Define(new MicroflowVariableDefinition
            {
                Name = pair.Key,
                Value = pair.Value with { },
                DataTypeJson = pair.Value.DataTypeJson,
                ScopeKind = MicroflowVariableScopeKind.ParallelBranch,
                AllowRedeclare = true
            });
        }

        return forked;
    }
}

public sealed class DefaultBranchMergePolicy : IBranchMergePolicy
{
    public BranchMergeResult Merge(IMicroflowVariableStore target, IReadOnlyList<BranchExecutionContext> branches)
    {
        var merged = new List<BranchMergedVariable>();
        foreach (var branch in branches)
        {
            foreach (var variableName in branch.WrittenVariableNames.OrderBy(name => name, StringComparer.Ordinal))
            {
                if (!branch.VariableStore.TryGet(variableName, out var value) || value is null)
                {
                    continue;
                }

                var mergedValue = value with
                {
                    Name = variableName,
                    ScopeKind = MicroflowVariableScopeKind.BranchMerge,
                    SourceKind = string.IsNullOrWhiteSpace(value.SourceKind) || string.Equals(value.SourceKind, MicroflowVariableSourceKind.Unknown, StringComparison.Ordinal)
                        ? MicroflowVariableSourceKind.ActionOutput
                        : value.SourceKind
                };
                if (target.Exists(variableName))
                {
                    target.Set(variableName, mergedValue);
                }
                else
                {
                    target.Define(new MicroflowVariableDefinition
                    {
                        Name = variableName,
                        Value = mergedValue,
                        DataTypeJson = mergedValue.DataTypeJson,
                        ScopeKind = MicroflowVariableScopeKind.BranchMerge,
                        AllowRedeclare = true
                    });
                }

                merged.Add(new BranchMergedVariable
                {
                    BranchId = branch.BranchId,
                    VariableName = variableName,
                    Value = mergedValue
                });
            }
        }

        return new BranchMergeResult { Variables = merged };
    }
}
