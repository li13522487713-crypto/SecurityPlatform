using Atlas.Application.Microflows.Runtime.Debug;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowDebugCoordinatorTests
{
    [Fact]
    public async Task WaitAtSafePoint_then_ReleaseOnePause_unblocks()
    {
        var store = new InMemoryDebugSessionStore();
        var coordinator = new MicroflowDebugCoordinator(store);
        var session = store.Create("mf-1");

        var waitTask = coordinator.WaitAtSafePointAsync(
            session.Id,
            "engine-run-1",
            new MicroflowDebugSafePoint(MicroflowDebugPausePhase.BeforeNode, "node-a", "startEvent", null),
            CancellationToken.None);

        Assert.False(waitTask.IsCompleted);

        coordinator.ReleaseOnePause(session.Id);

        await waitTask;

        Assert.True(waitTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task RunToNode_pauses_only_at_target_node()
    {
        var store = new InMemoryDebugSessionStore();
        var coordinator = new MicroflowDebugCoordinator(store);
        var session = store.Create("mf-1");

        coordinator.ApplyCommand(session.Id, new DebugCommand { Command = DebugCommandKind.RunToNode, TargetNodeObjectId = "node-b" });

        await coordinator.WaitAtSafePointAsync(
            session.Id,
            "engine-run-1",
            new MicroflowDebugSafePoint(MicroflowDebugPausePhase.BeforeNode, "node-a", "actionActivity", null),
            CancellationToken.None);
        Assert.Equal(MicroflowDebugSessionLifecycle.Running, store.Get(session.Id)?.Status);

        var waitTask = coordinator.WaitAtSafePointAsync(
            session.Id,
            "engine-run-1",
            new MicroflowDebugSafePoint(MicroflowDebugPausePhase.BeforeNode, "node-b", "actionActivity", "flow-b"),
            CancellationToken.None);
        Assert.False(waitTask.IsCompleted);

        coordinator.ApplyCommand(session.Id, new DebugCommand { Command = DebugCommandKind.Continue });
        await waitTask;
        Assert.Equal(MicroflowDebugSessionLifecycle.Running, store.Get(session.Id)?.Status);
    }

    [Fact]
    public async Task Cancel_releases_waiting_safe_point_and_marks_session_cancelled()
    {
        var store = new InMemoryDebugSessionStore();
        var coordinator = new MicroflowDebugCoordinator(store);
        var session = store.Create("mf-1");

        var waitTask = coordinator.WaitAtSafePointAsync(
            session.Id,
            "engine-run-1",
            new MicroflowDebugSafePoint(MicroflowDebugPausePhase.BeforeNode, "node-a", "startEvent", null),
            CancellationToken.None);
        Assert.False(waitTask.IsCompleted);

        coordinator.ApplyCommand(session.Id, new DebugCommand { Command = DebugCommandKind.Cancel });
        await waitTask;

        Assert.Equal(MicroflowDebugSessionLifecycle.Cancelled, store.Get(session.Id)?.Status);
    }

    [Fact]
    public void StepOver_sets_stepping_state_and_records_command_trace()
    {
        var store = new InMemoryDebugSessionStore();
        var coordinator = new MicroflowDebugCoordinator(store);
        var session = store.Create("mf-1");

        var updated = coordinator.ApplyCommand(session.Id, new DebugCommand { Command = DebugCommandKind.StepOver });

        Assert.Equal(MicroflowDebugSessionLifecycle.Stepping, updated?.Status);
        Assert.Equal(DebugCommandKind.StepOver, updated?.LastCommand);
        Assert.Contains(updated?.Trace ?? [], item => item.Kind == "command" && item.Message == DebugCommandKind.StepOver);
    }

    [Fact]
    public async Task StepInto_from_call_microflow_pauses_inside_child_run()
    {
        var store = new InMemoryDebugSessionStore();
        var coordinator = new MicroflowDebugCoordinator(store);
        var session = store.Create("mf-parent");

        var anchorWait = coordinator.WaitAtSafePointAsync(
            session.Id,
            "parent-run",
            new MicroflowDebugSafePoint(MicroflowDebugPausePhase.BeforeCallMicroflow, "call-child", "actionActivity", "f-call")
            {
                CallDepth = 0,
                SemanticKind = "callMicroflow"
            },
            CancellationToken.None);
        Assert.False(anchorWait.IsCompleted);

        coordinator.ApplyCommand(session.Id, new DebugCommand { Command = DebugCommandKind.StepInto });
        await anchorWait;

        var childWait = coordinator.WaitAtSafePointAsync(
            session.Id,
            "child-run",
            new MicroflowDebugSafePoint(MicroflowDebugPausePhase.BeforeNode, "child-start", "startEvent", null)
            {
                CallDepth = 1,
                SemanticKind = "startEvent"
            },
            CancellationToken.None);
        Assert.False(childWait.IsCompleted);

        coordinator.ApplyCommand(session.Id, new DebugCommand { Command = DebugCommandKind.Continue });
        await childWait;
    }

    [Fact]
    public async Task StepOver_from_call_microflow_skips_child_and_pauses_after_parent_call()
    {
        var store = new InMemoryDebugSessionStore();
        var coordinator = new MicroflowDebugCoordinator(store);
        var session = store.Create("mf-parent");

        var anchorWait = coordinator.WaitAtSafePointAsync(
            session.Id,
            "parent-run",
            new MicroflowDebugSafePoint(MicroflowDebugPausePhase.BeforeCallMicroflow, "call-child", "actionActivity", "f-call")
            {
                CallDepth = 0,
                SemanticKind = "callMicroflow"
            },
            CancellationToken.None);
        Assert.False(anchorWait.IsCompleted);

        coordinator.ApplyCommand(session.Id, new DebugCommand { Command = DebugCommandKind.StepOver });
        await anchorWait;

        await coordinator.WaitAtSafePointAsync(
            session.Id,
            "child-run",
            new MicroflowDebugSafePoint(MicroflowDebugPausePhase.BeforeNode, "child-start", "startEvent", null)
            {
                CallDepth = 1,
                SemanticKind = "startEvent"
            },
            CancellationToken.None);
        Assert.Equal(MicroflowDebugSessionLifecycle.Running, store.Get(session.Id)?.Status);

        var parentWait = coordinator.WaitAtSafePointAsync(
            session.Id,
            "parent-run",
            new MicroflowDebugSafePoint(MicroflowDebugPausePhase.AfterCallMicroflow, "call-child", "actionActivity", "f-call")
            {
                CallDepth = 0,
                SemanticKind = "callMicroflow"
            },
            CancellationToken.None);
        Assert.False(parentWait.IsCompleted);

        coordinator.ApplyCommand(session.Id, new DebugCommand { Command = DebugCommandKind.Continue });
        await parentWait;
    }

    [Fact]
    public async Task StepInto_from_rest_activity_pauses_before_rest_request()
    {
        var store = new InMemoryDebugSessionStore();
        var coordinator = new MicroflowDebugCoordinator(store);
        var session = store.Create("mf-1");

        var anchorWait = coordinator.WaitAtSafePointAsync(
            session.Id,
            "engine-run",
            new MicroflowDebugSafePoint(MicroflowDebugPausePhase.BeforeNode, "rest", "actionActivity", "f-rest")
            {
                CallDepth = 0,
                SemanticKind = "rest"
            },
            CancellationToken.None);
        Assert.False(anchorWait.IsCompleted);

        coordinator.ApplyCommand(session.Id, new DebugCommand { Command = DebugCommandKind.StepInto });
        await anchorWait;

        var restWait = coordinator.WaitAtSafePointAsync(
            session.Id,
            "engine-run",
            new MicroflowDebugSafePoint(MicroflowDebugPausePhase.BeforeRestRequest, "rest", "actionActivity", "f-rest")
            {
                CallDepth = 0,
                SemanticKind = "rest"
            },
            CancellationToken.None);
        Assert.False(restWait.IsCompleted);

        coordinator.ApplyCommand(session.Id, new DebugCommand { Command = DebugCommandKind.Continue });
        await restWait;
    }

    [Fact]
    public async Task StepOver_from_rest_activity_skips_internal_rest_phases_and_pauses_after_node()
    {
        var store = new InMemoryDebugSessionStore();
        var coordinator = new MicroflowDebugCoordinator(store);
        var session = store.Create("mf-1");

        var anchorWait = coordinator.WaitAtSafePointAsync(
            session.Id,
            "engine-run",
            new MicroflowDebugSafePoint(MicroflowDebugPausePhase.BeforeNode, "rest", "actionActivity", "f-rest")
            {
                CallDepth = 0,
                SemanticKind = "rest"
            },
            CancellationToken.None);
        Assert.False(anchorWait.IsCompleted);

        coordinator.ApplyCommand(session.Id, new DebugCommand { Command = DebugCommandKind.StepOver });
        await anchorWait;

        await coordinator.WaitAtSafePointAsync(
            session.Id,
            "engine-run",
            new MicroflowDebugSafePoint(MicroflowDebugPausePhase.BeforeRestRequest, "rest", "actionActivity", "f-rest")
            {
                CallDepth = 0,
                SemanticKind = "rest"
            },
            CancellationToken.None);
        await coordinator.WaitAtSafePointAsync(
            session.Id,
            "engine-run",
            new MicroflowDebugSafePoint(MicroflowDebugPausePhase.AfterRestHandled, "rest", "actionActivity", "f-rest")
            {
                CallDepth = 0,
                SemanticKind = "rest"
            },
            CancellationToken.None);
        Assert.Equal(MicroflowDebugSessionLifecycle.Running, store.Get(session.Id)?.Status);

        var afterNodeWait = coordinator.WaitAtSafePointAsync(
            session.Id,
            "engine-run",
            new MicroflowDebugSafePoint(MicroflowDebugPausePhase.AfterNode, "rest", "actionActivity", "f-rest")
            {
                CallDepth = 0,
                SemanticKind = "rest"
            },
            CancellationToken.None);
        Assert.False(afterNodeWait.IsCompleted);

        coordinator.ApplyCommand(session.Id, new DebugCommand { Command = DebugCommandKind.Continue });
        await afterNodeWait;
    }

    [Fact]
    public async Task StepOut_pauses_when_runtime_returns_to_parent_depth()
    {
        var store = new InMemoryDebugSessionStore();
        var coordinator = new MicroflowDebugCoordinator(store);
        var session = store.Create("mf-child");

        var anchorWait = coordinator.WaitAtSafePointAsync(
            session.Id,
            "child-run",
            new MicroflowDebugSafePoint(MicroflowDebugPausePhase.BeforeNode, "child-node", "actionActivity", null)
            {
                CallDepth = 1,
                SemanticKind = "activity"
            },
            CancellationToken.None);
        Assert.False(anchorWait.IsCompleted);

        coordinator.ApplyCommand(session.Id, new DebugCommand { Command = DebugCommandKind.StepOut });
        await anchorWait;

        await coordinator.WaitAtSafePointAsync(
            session.Id,
            "child-run",
            new MicroflowDebugSafePoint(MicroflowDebugPausePhase.AfterNode, "child-node", "actionActivity", null)
            {
                CallDepth = 1,
                SemanticKind = "activity"
            },
            CancellationToken.None);
        Assert.Equal(MicroflowDebugSessionLifecycle.Running, store.Get(session.Id)?.Status);

        var parentWait = coordinator.WaitAtSafePointAsync(
            session.Id,
            "parent-run",
            new MicroflowDebugSafePoint(MicroflowDebugPausePhase.AfterCallMicroflow, "call-child", "actionActivity", "f-call")
            {
                CallDepth = 0,
                SemanticKind = "callMicroflow"
            },
            CancellationToken.None);
        Assert.False(parentWait.IsCompleted);

        coordinator.ApplyCommand(session.Id, new DebugCommand { Command = DebugCommandKind.Continue });
        await parentWait;
    }

    [Fact]
    public async Task Stale_breakpoint_does_not_pause_runtime()
    {
        var store = new InMemoryDebugSessionStore();
        var coordinator = new MicroflowDebugCoordinator(store);
        var session = store.Create("mf-1");
        store.Upsert(session with
        {
            LastCommand = DebugCommandKind.Continue,
            Breakpoints =
            [
                new BreakpointDescriptor("bp-stale", "node-a", BreakpointScope.Node, Stale: true)
            ]
        });

        await coordinator.WaitAtSafePointAsync(
            session.Id,
            "engine-run-1",
            new MicroflowDebugSafePoint(MicroflowDebugPausePhase.BeforeNode, "node-a", "actionActivity", null),
            CancellationToken.None);

        Assert.Equal(MicroflowDebugSessionLifecycle.Running, store.Get(session.Id)?.Status);
    }
}
