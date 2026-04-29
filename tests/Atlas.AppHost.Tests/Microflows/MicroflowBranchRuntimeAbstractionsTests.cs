using Atlas.Application.Microflows.Runtime.Branches;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowBranchRuntimeAbstractionsTests
{
    [Fact]
    public async Task SequentialSchedulerStopsAfterFirstFailure()
    {
        var scheduler = new SequentialBranchScheduler();
        var visited = new List<string>();
        var branches = new[]
        {
            Branch("a", visited, success: true),
            Branch("b", visited, success: false),
            Branch("c", visited, success: true),
        };

        var results = await scheduler.RunAsync(branches, CancellationToken.None);

        Assert.Equal("sequential", scheduler.Mode);
        Assert.Equal(["a", "b"], visited);
        Assert.Equal(["a", "b"], results.Select(result => result.BranchId));
        Assert.False(results[^1].Success);
    }

    [Fact]
    public async Task ParallelSchedulerRunsAllBranchesAndCapturesFailures()
    {
        var scheduler = new ParallelBranchScheduler();
        var branches = new[]
        {
            Branch("a", success: true),
            Branch("b", success: false),
            Branch("c", success: true),
        };

        var results = await scheduler.RunAsync(branches, CancellationToken.None);

        Assert.Equal("trueParallel", scheduler.Mode);
        Assert.Equal(["a", "b", "c"], results.Select(result => result.BranchId).Order());
        Assert.Contains(results, result => result.BranchId == "b" && !result.Success);
    }

    [Fact]
    public async Task ParallelSplitActivatesAllBranchesAndMarksCompletedTokens()
    {
        var store = new InMemoryGatewayJoinStateStore();
        var split = new ParallelGatewaySplitExecutor(new ParallelBranchScheduler(), store);
        var sourceToken = new GatewayToken { SplitInstanceId = "split-1", BranchId = "source" };

        var state = await split.ExecuteAsync(
            sourceToken,
            [
                Branch("a", success: true),
                Branch("b", success: true),
                Branch("c", success: true)
            ],
            CancellationToken.None);

        Assert.Equal("completed", state.Status);
        Assert.Equal(["a", "b", "c"], state.ArrivedTokens.Order());
        Assert.Equal(["a", "b", "c"], state.CompletedTokens.Order());
        Assert.Equal("completed", state.BranchStates["a"]);
    }

    [Fact]
    public async Task ParallelSplitMarksFailedBranchWithoutPretendingSuccess()
    {
        var store = new InMemoryGatewayJoinStateStore();
        var split = new ParallelGatewaySplitExecutor(new ParallelBranchScheduler(), store);

        var state = await split.ExecuteAsync(
            new GatewayToken { SplitInstanceId = "split-fail" },
            [
                Branch("ok", success: true),
                Branch("fail", success: false)
            ],
            CancellationToken.None);

        Assert.Equal("failed", state.Status);
        Assert.Contains("fail", state.FailedTokens);
        Assert.Equal("failed", state.BranchStates["fail"]);
    }

    [Fact]
    public void ParallelJoinWaitsForAllExpectedBranches()
    {
        var store = new InMemoryGatewayJoinStateStore();
        store.MarkArrived("split-join", "a");
        store.MarkArrived("split-join", "b");
        store.MarkCompleted("split-join", "a");
        var join = new ParallelGatewayJoinExecutor(store);

        var canContinueBeforeB = join.CanContinue("split-join", new HashSet<string>(["a", "b"], StringComparer.Ordinal), out var waitingState);
        store.MarkCompleted("split-join", "b");
        var canContinueAfterB = join.CanContinue("split-join", new HashSet<string>(["a", "b"], StringComparer.Ordinal), out var completedState);

        Assert.False(canContinueBeforeB);
        Assert.Equal(["a"], waitingState.CompletedTokens);
        Assert.True(canContinueAfterB);
        Assert.Equal("completed", completedState.Status);
    }

    [Fact]
    public void ParallelJoinDoesNotContinueWhenAnyBranchFailedOrCancelled()
    {
        var failedStore = new InMemoryGatewayJoinStateStore();
        failedStore.MarkArrived("split-failed", "a");
        failedStore.MarkArrived("split-failed", "b");
        failedStore.MarkCompleted("split-failed", "a");
        failedStore.MarkFailed("split-failed", "b");
        var failedJoin = new ParallelGatewayJoinExecutor(failedStore);

        var cancelledStore = new InMemoryGatewayJoinStateStore();
        cancelledStore.MarkArrived("split-cancelled", "a");
        cancelledStore.MarkArrived("split-cancelled", "b");
        cancelledStore.MarkCompleted("split-cancelled", "a");
        cancelledStore.MarkCancelled("split-cancelled", "b");
        var cancelledJoin = new ParallelGatewayJoinExecutor(cancelledStore);

        Assert.False(failedJoin.CanContinue("split-failed", new HashSet<string>(["a", "b"], StringComparer.Ordinal), out var failedState));
        Assert.Equal("failed", failedState.Status);
        Assert.False(cancelledJoin.CanContinue("split-cancelled", new HashSet<string>(["a", "b"], StringComparer.Ordinal), out var cancelledState));
        Assert.Equal("cancelled", cancelledState.Status);
    }

    [Fact]
    public void InclusiveSplitBuildsActivationSetForAllActiveBranches()
    {
        var split = new InclusiveGatewaySplitExecutor();

        var activation = split.BuildActivationSet("inclusive-1", [
            ("a", true, false),
            ("b", false, false),
            ("c", true, false)
        ]);

        Assert.Equal("inclusive-1", activation.SplitInstanceId);
        Assert.Equal(["a", "c"], activation.ActiveBranchIds.Order());
    }

    [Fact]
    public void InclusiveSplitFallsBackToOtherwiseWhenNoConditionMatches()
    {
        var split = new InclusiveGatewaySplitExecutor();

        var activation = split.BuildActivationSet("inclusive-2", [
            ("a", false, false),
            ("otherwise", false, true)
        ]);

        Assert.Equal(["otherwise"], activation.ActiveBranchIds);
    }

    [Fact]
    public void InclusiveSplitRejectsDuplicateOtherwise()
    {
        var split = new InclusiveGatewaySplitExecutor();

        var ex = Assert.Throws<InvalidOperationException>(() => split.BuildActivationSet("inclusive-3", [
            ("a", false, true),
            ("b", false, true)
        ]));

        Assert.Equal("INCLUSIVE_OTHERWISE_NOT_UNIQUE", ex.Message);
    }

    [Fact]
    public void InclusiveSplitRejectsNoBranchSelectedWithoutOtherwise()
    {
        var split = new InclusiveGatewaySplitExecutor();

        var ex = Assert.Throws<InvalidOperationException>(() => split.BuildActivationSet("inclusive-4", [
            ("a", false, false),
            ("b", false, false)
        ]));

        Assert.Equal("INCLUSIVE_NO_BRANCH_SELECTED", ex.Message);
    }

    [Fact]
    public void InclusiveJoinWaitsOnlyForActivatedBranches()
    {
        var store = new InMemoryGatewayJoinStateStore();
        store.MarkArrived("inclusive-join", "a");
        store.MarkArrived("inclusive-join", "b");
        store.MarkCompleted("inclusive-join", "a");
        var join = new InclusiveGatewayJoinExecutor(store);
        var activation = new ActivationSet
        {
            SplitInstanceId = "inclusive-join",
            ActiveBranchIds = new HashSet<string>(["a"], StringComparer.Ordinal)
        };

        var canContinue = join.CanContinue(activation, out var state);

        Assert.True(canContinue);
        Assert.Equal("running", state.Status);
        Assert.Equal(["a"], state.CompletedTokens);
    }

    [Fact]
    public void InclusiveJoinWaitsForAllActiveBranches()
    {
        var store = new InMemoryGatewayJoinStateStore();
        store.MarkArrived("inclusive-wait", "a");
        store.MarkArrived("inclusive-wait", "b");
        store.MarkCompleted("inclusive-wait", "a");
        var join = new InclusiveGatewayJoinExecutor(store);
        var activation = new ActivationSet
        {
            SplitInstanceId = "inclusive-wait",
            ActiveBranchIds = new HashSet<string>(["a", "b"], StringComparer.Ordinal)
        };

        var canContinueBeforeB = join.CanContinue(activation, out _);
        store.MarkCompleted("inclusive-wait", "b");
        var canContinueAfterB = join.CanContinue(activation, out var state);

        Assert.False(canContinueBeforeB);
        Assert.True(canContinueAfterB);
        Assert.Equal("completed", state.Status);
    }

    [Fact]
    public void JoinStateStoreTracksArrivedCompletedFailedAndCancelledBranches()
    {
        var store = new InMemoryGatewayJoinStateStore();
        store.MarkArrived("split-1", "a");
        store.MarkArrived("split-1", "b");
        store.MarkCompleted("split-1", "a");
        store.MarkFailed("split-1", "b");
        store.MarkCancelled("split-1", "c");

        var state = store.Get("split-1");
        var runtimeState = GatewayRuntimeState.FromJoinState(state);

        Assert.Equal(["a", "b"], state.ArrivedBranchIds.Order());
        Assert.Equal(["a"], state.CompletedBranchIds);
        Assert.Equal(["b"], state.FailedBranchIds);
        Assert.Equal(["c"], state.CancelledBranchIds);
        Assert.Equal("failed", runtimeState.Status);
        Assert.Equal("completed", runtimeState.BranchStates["a"]);
        Assert.Equal("failed", runtimeState.BranchStates["b"]);
        Assert.Equal("cancelled", runtimeState.BranchStates["c"]);
    }

    [Fact]
    public void GatewayRuntimeStatePreservesAsyncDebugAndCancelIdentifiers()
    {
        var state = new GatewayRuntimeState
        {
            RunId = "run-1",
            MicroflowId = "mf-1",
            GatewayId = "gateway-1",
            SplitInstanceId = "split-1",
            JoinGatewayId = "join-1",
            ParentSplitInstanceId = "parent-split",
            LoopIterationId = "loop-0",
            CallStackFrameId = "call-1",
            ActivationSet = new HashSet<string>(["a", "b"], StringComparer.Ordinal),
            ArrivedTokens = new HashSet<string>(["a", "b"], StringComparer.Ordinal),
            CompletedTokens = new HashSet<string>(["a"], StringComparer.Ordinal),
            FailedTokens = new HashSet<string>(["b"], StringComparer.Ordinal),
            CancelledTokens = new HashSet<string>(["c"], StringComparer.Ordinal),
            BranchStates = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["a"] = "completed",
                ["b"] = "failed",
                ["c"] = "cancelled"
            },
            Status = "failed"
        };

        Assert.Equal("run-1", state.RunId);
        Assert.Equal("mf-1", state.MicroflowId);
        Assert.Equal("gateway-1", state.GatewayId);
        Assert.Equal("join-1", state.JoinGatewayId);
        Assert.Equal("parent-split", state.ParentSplitInstanceId);
        Assert.Equal("loop-0", state.LoopIterationId);
        Assert.Equal("call-1", state.CallStackFrameId);
        Assert.Equal(["a", "b"], state.ActivationSet.Order());
        Assert.Equal(["c"], state.CancelledTokens);
        Assert.Equal("cancelled", state.BranchStates["c"]);
    }

    [Fact]
    public void GatewayTokenCarriesLoopIterationAndCallStackFrameIsolation()
    {
        var token = new GatewayToken
        {
            SplitInstanceId = "split-loop-call",
            BranchId = "branch-a",
            LoopIterationId = "loop-iteration-3",
            CallStackFrameId = "call-frame-2"
        };

        Assert.Equal("split-loop-call", token.SplitInstanceId);
        Assert.Equal("loop-iteration-3", token.LoopIterationId);
        Assert.Equal("call-frame-2", token.CallStackFrameId);
    }

    [Fact]
    public async Task ParallelSchedulerPropagatesCancellationToBranches()
    {
        var scheduler = new ParallelBranchScheduler();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => scheduler.RunAsync(
            [
                new MicroflowBranchExecutionRequest
                {
                    BranchId = "cancelled",
                    ExecuteAsync = token =>
                    {
                        token.ThrowIfCancellationRequested();
                        return Task.FromResult(new MicroflowBranchExecutionResult { BranchId = "cancelled", Success = true });
                    }
                }
            ],
            cts.Token));
    }


    [Fact]
    public void BranchUnitOfWorkFactoryCreatesIndependentUnitOfWork()
    {
        var factory = new DefaultBranchUnitOfWorkFactory();

        var first = factory.Create("branch-a");
        var second = factory.Create("branch-b");

        Assert.Equal("branch-a", first.BranchId);
        Assert.Equal("branch-b", second.BranchId);
        Assert.NotSame(first.UnitOfWork, second.UnitOfWork);
    }

    [Fact]
    public void WriteConflictDetectorFindsVariableAndObjectMemberConflicts()
    {
        var conflicts = GatewayWriteConflictDetector.Detect([
            new BranchWriteIntent { BranchId = "a", VariableName = "shared" },
            new BranchWriteIntent { BranchId = "b", VariableName = "shared" },
            new BranchWriteIntent { BranchId = "a", ObjectId = "order-1", MemberName = "Amount" },
            new BranchWriteIntent { BranchId = "b", ObjectId = "order-1", MemberName = "Amount" },
            new BranchWriteIntent { BranchId = "c", ObjectId = "order-2", MemberName = "Status" }
        ]);

        Assert.Contains("PARALLEL_VARIABLE_WRITE_CONFLICT:shared", conflicts);
        Assert.Contains("PARALLEL_WRITE_CONFLICT:order-1:Amount", conflicts);
        Assert.DoesNotContain(conflicts, conflict => conflict.Contains("order-2", StringComparison.Ordinal));
    }

    private static MicroflowBranchExecutionRequest Branch(string id, List<string> visited, bool success)
        => new()
        {
            BranchId = id,
            ExecuteAsync = _ =>
            {
                visited.Add(id);
                return Task.FromResult(new MicroflowBranchExecutionResult { BranchId = id, Success = success });
            }
        };

    private static MicroflowBranchExecutionRequest Branch(string id, bool success)
        => new()
        {
            BranchId = id,
            ExecuteAsync = _ => Task.FromResult(new MicroflowBranchExecutionResult { BranchId = id, Success = success })
        };
}
