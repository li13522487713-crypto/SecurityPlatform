using System.Collections.Concurrent;
using Atlas.Application.Microflows.Runtime.Transactions;

namespace Atlas.Application.Microflows.Runtime.Branches;

public sealed record MicroflowBranchExecutionRequest
{
    public string BranchId { get; init; } = Guid.NewGuid().ToString("N");
    public string? SplitInstanceId { get; init; }
    public Func<CancellationToken, Task<MicroflowBranchExecutionResult>> ExecuteAsync { get; init; } = _ => Task.FromResult(new MicroflowBranchExecutionResult());
}

public sealed record MicroflowBranchExecutionResult
{
    public string BranchId { get; init; } = string.Empty;
    public bool Success { get; init; } = true;
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

public interface IBranchScheduler
{
    string Mode { get; }

    Task<IReadOnlyList<MicroflowBranchExecutionResult>> RunAsync(
        IReadOnlyList<MicroflowBranchExecutionRequest> branches,
        CancellationToken cancellationToken);
}

public sealed class SequentialBranchScheduler : IBranchScheduler
{
    public string Mode => "sequential";

    public async Task<IReadOnlyList<MicroflowBranchExecutionResult>> RunAsync(
        IReadOnlyList<MicroflowBranchExecutionRequest> branches,
        CancellationToken cancellationToken)
    {
        var results = new List<MicroflowBranchExecutionResult>(branches.Count);
        foreach (var branch in branches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await branch.ExecuteAsync(cancellationToken);
            results.Add(result with { BranchId = string.IsNullOrWhiteSpace(result.BranchId) ? branch.BranchId : result.BranchId });
            if (!result.Success)
            {
                break;
            }
        }

        return results;
    }
}

public interface IBranchUnitOfWork
{
    string BranchId { get; }

    IMicroflowUnitOfWork UnitOfWork { get; }

    void Commit(string? reason = null);

    void Rollback(string? reason = null);
}

public sealed class BranchUnitOfWork : IBranchUnitOfWork
{
    public BranchUnitOfWork(string branchId, IMicroflowUnitOfWork? unitOfWork = null)
    {
        BranchId = string.IsNullOrWhiteSpace(branchId) ? Guid.NewGuid().ToString("N") : branchId;
        UnitOfWork = unitOfWork ?? new MicroflowUnitOfWork();
    }

    public string BranchId { get; }

    public IMicroflowUnitOfWork UnitOfWork { get; }

    public void Commit(string? reason = null) => UnitOfWork.MarkCommitted(reason);

    public void Rollback(string? reason = null) => UnitOfWork.MarkRolledBack(reason);
}

public sealed record GatewayJoinState
{
    public string SplitInstanceId { get; init; } = string.Empty;
    public IReadOnlySet<string> ArrivedBranchIds { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    public IReadOnlySet<string> CompletedBranchIds { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    public IReadOnlySet<string> FailedBranchIds { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    public IReadOnlySet<string> CancelledBranchIds { get; init; } = new HashSet<string>(StringComparer.Ordinal);
}

public interface IGatewayJoinStateStore
{
    GatewayJoinState MarkArrived(string splitInstanceId, string branchId);

    GatewayJoinState MarkCompleted(string splitInstanceId, string branchId);

    GatewayJoinState MarkFailed(string splitInstanceId, string branchId);

    GatewayJoinState MarkCancelled(string splitInstanceId, string branchId);

    GatewayJoinState Get(string splitInstanceId);
}

public sealed class InMemoryGatewayJoinStateStore : IGatewayJoinStateStore
{
    private readonly ConcurrentDictionary<string, MutableGatewayJoinState> _states = new(StringComparer.Ordinal);

    public GatewayJoinState MarkArrived(string splitInstanceId, string branchId)
        => Mutate(splitInstanceId, state => state.Arrived.Add(branchId));

    public GatewayJoinState MarkCompleted(string splitInstanceId, string branchId)
        => Mutate(splitInstanceId, state => state.Completed.Add(branchId));

    public GatewayJoinState MarkFailed(string splitInstanceId, string branchId)
        => Mutate(splitInstanceId, state => state.Failed.Add(branchId));

    public GatewayJoinState MarkCancelled(string splitInstanceId, string branchId)
        => Mutate(splitInstanceId, state => state.Cancelled.Add(branchId));

    public GatewayJoinState Get(string splitInstanceId)
        => Snapshot(_states.GetOrAdd(splitInstanceId, static key => new MutableGatewayJoinState(key)));

    private GatewayJoinState Mutate(string splitInstanceId, Action<MutableGatewayJoinState> mutate)
    {
        var state = _states.GetOrAdd(splitInstanceId, static key => new MutableGatewayJoinState(key));
        lock (state)
        {
            mutate(state);
            return Snapshot(state);
        }
    }

    private static GatewayJoinState Snapshot(MutableGatewayJoinState state)
        => new()
        {
            SplitInstanceId = state.SplitInstanceId,
            ArrivedBranchIds = state.Arrived.ToHashSet(StringComparer.Ordinal),
            CompletedBranchIds = state.Completed.ToHashSet(StringComparer.Ordinal),
            FailedBranchIds = state.Failed.ToHashSet(StringComparer.Ordinal),
            CancelledBranchIds = state.Cancelled.ToHashSet(StringComparer.Ordinal)
        };

    private sealed class MutableGatewayJoinState
    {
        public MutableGatewayJoinState(string splitInstanceId)
        {
            SplitInstanceId = splitInstanceId;
        }

        public string SplitInstanceId { get; }
        public HashSet<string> Arrived { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Completed { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Failed { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Cancelled { get; } = new(StringComparer.Ordinal);
    }
}
