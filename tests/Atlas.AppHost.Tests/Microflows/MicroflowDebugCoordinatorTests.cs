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
}
