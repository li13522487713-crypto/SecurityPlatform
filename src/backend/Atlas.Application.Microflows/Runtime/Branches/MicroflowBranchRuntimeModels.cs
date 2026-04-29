using System.Collections.Concurrent;
using Atlas.Application.Microflows.Runtime.Transactions;

namespace Atlas.Application.Microflows.Runtime.Branches;

public sealed record GatewayToken
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string SplitInstanceId { get; init; } = string.Empty;
    public string BranchId { get; init; } = string.Empty;
    public string? LoopIterationId { get; init; }
    public string? CallStackFrameId { get; init; }
    public string Status { get; init; } = GatewayTokenStatus.Created;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; init; }
}

public static class GatewayTokenStatus
{
    public const string Created = "created";
    public const string Arrived = "arrived";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
    public const string Handled = "handled";
}

public sealed record GatewayTokenSet
{
    public string SplitInstanceId { get; init; } = string.Empty;
    public IReadOnlyList<GatewayToken> Tokens { get; init; } = Array.Empty<GatewayToken>();

    public IReadOnlyList<GatewayToken> ActiveTokens => Tokens
        .Where(static token => token.Status is GatewayTokenStatus.Created or GatewayTokenStatus.Arrived)
        .ToArray();

    public bool IsComplete => Tokens.Count > 0 && Tokens.All(static token =>
        token.Status is GatewayTokenStatus.Completed or GatewayTokenStatus.Failed or GatewayTokenStatus.Cancelled or GatewayTokenStatus.Handled);
}

public readonly record struct SplitInstanceId(string Value)
{
    public static SplitInstanceId New(string? prefix = null)
        => new($"{(string.IsNullOrWhiteSpace(prefix) ? "split" : prefix)}-{Guid.NewGuid():N}");

    public override string ToString() => Value;
}

public sealed record ActivationSet
{
    public string SplitInstanceId { get; init; } = string.Empty;
    public IReadOnlySet<string> ActiveBranchIds { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    public string? OtherwiseBranchId { get; init; }

    public bool Contains(string branchId) => ActiveBranchIds.Contains(branchId);
}

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

public sealed record BranchWriteIntent
{
    public string BranchId { get; init; } = string.Empty;
    public string? VariableName { get; init; }
    public string? ObjectId { get; init; }
    public string? MemberName { get; init; }
}

public static class GatewayWriteConflictDetector
{
    public const string ParallelVariableWriteConflict = "PARALLEL_VARIABLE_WRITE_CONFLICT";
    public const string ParallelWriteConflict = "PARALLEL_WRITE_CONFLICT";

    public static IReadOnlyList<string> Detect(IReadOnlyList<BranchWriteIntent> intents)
    {
        var conflicts = new List<string>();
        conflicts.AddRange(FindDuplicateKeys(
            intents.Where(static intent => !string.IsNullOrWhiteSpace(intent.VariableName)),
            static intent => intent.VariableName!,
            ParallelVariableWriteConflict));
        conflicts.AddRange(FindDuplicateKeys(
            intents.Where(static intent => !string.IsNullOrWhiteSpace(intent.ObjectId)),
            static intent => $"{intent.ObjectId}:{intent.MemberName ?? "*"}",
            ParallelWriteConflict));
        return conflicts;
    }

    private static IEnumerable<string> FindDuplicateKeys(
        IEnumerable<BranchWriteIntent> intents,
        Func<BranchWriteIntent, string> keySelector,
        string code)
        => intents
            .GroupBy(keySelector, StringComparer.Ordinal)
            .Where(group => group.Select(static item => item.BranchId).Distinct(StringComparer.Ordinal).Skip(1).Any())
            .Select(group => $"{code}:{group.Key}");
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

public sealed class ParallelBranchScheduler : IBranchScheduler
{
    public string Mode => "trueParallel";

    public async Task<IReadOnlyList<MicroflowBranchExecutionResult>> RunAsync(
        IReadOnlyList<MicroflowBranchExecutionRequest> branches,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tasks = branches.Select(async branch =>
        {
            try
            {
                var result = await branch.ExecuteAsync(cancellationToken);
                return result with { BranchId = string.IsNullOrWhiteSpace(result.BranchId) ? branch.BranchId : result.BranchId };
            }
            catch (OperationCanceledException)
            {
                return new MicroflowBranchExecutionResult
                {
                    BranchId = branch.BranchId,
                    Success = false,
                    ErrorCode = "BRANCH_CANCELLED",
                    ErrorMessage = "Branch execution was cancelled."
                };
            }
            catch (Exception ex)
            {
                return new MicroflowBranchExecutionResult
                {
                    BranchId = branch.BranchId,
                    Success = false,
                    ErrorCode = "BRANCH_FAILED",
                    ErrorMessage = ex.Message
                };
            }
        }).ToArray();
        return await Task.WhenAll(tasks);
    }
}

public sealed class ParallelGatewaySplitExecutor
{
    private readonly IBranchScheduler _scheduler;
    private readonly IGatewayJoinStateStore _joinStateStore;

    public ParallelGatewaySplitExecutor(IBranchScheduler scheduler, IGatewayJoinStateStore joinStateStore)
    {
        _scheduler = scheduler;
        _joinStateStore = joinStateStore;
    }

    public async Task<GatewayRuntimeState> ExecuteAsync(
        GatewayToken sourceToken,
        IReadOnlyList<MicroflowBranchExecutionRequest> branches,
        CancellationToken cancellationToken)
    {
        var splitInstanceId = string.IsNullOrWhiteSpace(sourceToken.SplitInstanceId)
            ? Guid.NewGuid().ToString("N")
            : sourceToken.SplitInstanceId;
        var requests = branches.Select(branch => branch with { SplitInstanceId = splitInstanceId }).ToArray();
        foreach (var branch in requests)
        {
            _joinStateStore.MarkArrived(splitInstanceId, branch.BranchId);
        }

        var results = await _scheduler.RunAsync(requests, cancellationToken);
        foreach (var result in results)
        {
            if (result.Success)
            {
                _joinStateStore.MarkCompleted(splitInstanceId, result.BranchId);
            }
            else if (string.Equals(result.ErrorCode, "CANCELLED", StringComparison.OrdinalIgnoreCase))
            {
                _joinStateStore.MarkCancelled(splitInstanceId, result.BranchId);
            }
            else
            {
                _joinStateStore.MarkFailed(splitInstanceId, result.BranchId);
            }
        }

        return GatewayRuntimeState.FromJoinState(_joinStateStore.Get(splitInstanceId));
    }
}

public sealed class ParallelGatewayJoinExecutor
{
    private readonly IGatewayJoinStateStore _joinStateStore;

    public ParallelGatewayJoinExecutor(IGatewayJoinStateStore joinStateStore)
    {
        _joinStateStore = joinStateStore;
    }

    public bool CanContinue(string splitInstanceId, IReadOnlySet<string> expectedBranchIds, out GatewayRuntimeState state)
    {
        state = GatewayRuntimeState.FromJoinState(_joinStateStore.Get(splitInstanceId));
        return expectedBranchIds.All(branchId => state.CompletedTokens.Contains(branchId))
               && !state.FailedTokens.Any()
               && !state.CancelledTokens.Any();
    }
}

public sealed class InclusiveGatewaySplitExecutor
{
    public ActivationSet BuildActivationSet(
        string splitInstanceId,
        IEnumerable<(string BranchId, bool Active, bool Otherwise)> branches)
    {
        var candidates = branches.ToArray();
        var otherwiseCount = candidates.Count(branch => branch.Otherwise);
        if (otherwiseCount > 1)
        {
            throw new InvalidOperationException("INCLUSIVE_OTHERWISE_NOT_UNIQUE");
        }

        var active = candidates.Where(branch => branch.Active).Select(branch => branch.BranchId).ToArray();
        if (active.Length == 0)
        {
            var otherwise = candidates.Where(branch => branch.Otherwise).Select(branch => branch.BranchId).ToArray();
            active = otherwise.Length == 1 ? otherwise : active;
        }

        if (active.Length == 0)
        {
            throw new InvalidOperationException("INCLUSIVE_NO_BRANCH_SELECTED");
        }

        return new ActivationSet(splitInstanceId, active);
    }
}

public sealed class InclusiveGatewayJoinExecutor
{
    private readonly IGatewayJoinStateStore _joinStateStore;

    public InclusiveGatewayJoinExecutor(IGatewayJoinStateStore joinStateStore)
    {
        _joinStateStore = joinStateStore;
    }

    public bool CanContinue(ActivationSet activationSet, out GatewayRuntimeState state)
    {
        state = GatewayRuntimeState.FromJoinState(_joinStateStore.Get(activationSet.SplitInstanceId));
        var activeBranchIds = activationSet.ActiveBranchIds;
        return activeBranchIds.All(branchId => state.CompletedTokens.Contains(branchId));
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
