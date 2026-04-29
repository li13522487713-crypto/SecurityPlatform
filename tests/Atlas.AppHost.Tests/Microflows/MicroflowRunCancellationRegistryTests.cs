using Atlas.Application.Microflows.Runtime;

namespace Atlas.AppHost.Tests.Microflows;

/// <summary>
/// P0-6 验证：cancellation registry 的语义。
///   1. Register 后 IsRegistered=true，Unregister 后变 false。
///   2. Cancel 触发对应 token 取消并返回 true；未登记的 runId 返回 false。
///   3. 外部 token 取消时也会取消 linked token。
/// </summary>
public sealed class MicroflowRunCancellationRegistryTests
{
    [Fact]
    public void Register_AssignsTokenAndUnregisterReleasesIt()
    {
        var registry = new MicroflowRunCancellationRegistry();
        var external = new CancellationTokenSource();

        var cts = registry.Register("run-1", external.Token);
        Assert.True(registry.IsRegistered("run-1"));
        Assert.False(cts.IsCancellationRequested);

        registry.Unregister("run-1");
        Assert.False(registry.IsRegistered("run-1"));
    }

    [Fact]
    public void Cancel_ReturnsFalseForUnknownRun()
    {
        var registry = new MicroflowRunCancellationRegistry();
        Assert.False(registry.Cancel("missing"));
    }

    [Fact]
    public void Cancel_TriggersLinkedToken()
    {
        var registry = new MicroflowRunCancellationRegistry();
        var external = new CancellationTokenSource();

        var cts = registry.Register("run-2", external.Token);
        Assert.True(registry.Cancel("run-2"));
        Assert.True(cts.IsCancellationRequested);
    }

    [Fact]
    public void ExternalCancel_PropagatesToLinkedToken()
    {
        var registry = new MicroflowRunCancellationRegistry();
        var external = new CancellationTokenSource();

        var cts = registry.Register("run-3", external.Token);
        external.Cancel();
        Assert.True(cts.IsCancellationRequested);
    }

    [Fact]
    public void Register_OverwritesPreviousHandleSafely()
    {
        var registry = new MicroflowRunCancellationRegistry();
        var external = new CancellationTokenSource();

        var first = registry.Register("run-4", external.Token);
        var second = registry.Register("run-4", external.Token);

        Assert.NotSame(first, second);
        Assert.True(registry.IsRegistered("run-4"));
        // first cts was disposed when overwritten; cancellation requests on second still work.
        Assert.True(registry.Cancel("run-4"));
        Assert.True(second.IsCancellationRequested);
    }
}
