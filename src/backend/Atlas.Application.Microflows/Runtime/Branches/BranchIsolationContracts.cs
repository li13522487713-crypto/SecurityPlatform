using Atlas.Application.Microflows.Runtime;

namespace Atlas.Application.Microflows.Runtime.Branches;

public sealed record BranchExecutionContext
{
    public string BranchId { get; init; } = Guid.NewGuid().ToString("N");

    public IMicroflowVariableStore VariableStore { get; init; } = new MicroflowVariableStore();
}

public interface IVariableScopeForker
{
    IMicroflowVariableStore Fork(IMicroflowVariableStore source, string branchId);
}

public interface IBranchMergePolicy
{
    void Merge(IMicroflowVariableStore target, IReadOnlyList<BranchExecutionContext> branches);
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
                AllowShadowing = true
            });
        }

        return forked;
    }
}

public sealed class NoOpBranchMergePolicy : IBranchMergePolicy
{
    public void Merge(IMicroflowVariableStore target, IReadOnlyList<BranchExecutionContext> branches)
    {
        _ = target;
        _ = branches;
    }
}
