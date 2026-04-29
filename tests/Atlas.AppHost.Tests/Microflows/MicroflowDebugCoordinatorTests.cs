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
