using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.SecurityPlatform.Tests.Services;

/// <summary>
/// TS-23: WorkflowExecution 领域实体 + Persistence 层行为测试。
/// 验证实体生命周期状态转换、字段赋值和 IsDebug 标志（PS-01）。
/// </summary>
public sealed class WorkflowExecutionEntityTests
{
    private static readonly TenantId TenantId = new TenantId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private const long WorkflowId = 1L;
    private const int VersionNumber = 1;
    private const long UserId = 100L;

    private static WorkflowExecution CreateExecution(bool isDebug = false, string? inputsJson = null)
        => new WorkflowExecution(
            tenantId: TenantId,
            workflowId: WorkflowId,
            versionNumber: VersionNumber,
            createdByUserId: UserId,
            inputsJson: inputsJson,
            id: 1001L,
            isDebug: isDebug);

    // ─── Initial State ────────────────────────────────────────────────────────

    [Fact]
    public void NewExecution_ShouldHavePendingStatus()
    {
        var exec = CreateExecution();
        Assert.Equal(ExecutionStatus.Pending, exec.Status);
    }

    [Fact]
    public void NewExecution_ShouldHaveCorrectWorkflowId()
    {
        var exec = CreateExecution();
        Assert.Equal(WorkflowId, exec.WorkflowId);
    }

    [Fact]
    public void NewExecution_ShouldHaveStartedAtSetToUtcNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var exec = CreateExecution();
        Assert.True(exec.StartedAt >= before);
        Assert.True(exec.StartedAt <= DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void NewExecution_ShouldHaveNoInterrupt()
    {
        var exec = CreateExecution();
        Assert.Equal(InterruptType.None, exec.InterruptType);
        Assert.Null(exec.InterruptNodeKey);
    }

    // ─── PS-01: IsDebug flag ──────────────────────────────────────────────────

    [Fact]
    public void NewExecution_WithIsDebugTrue_ShouldSetDebugFlag()
    {
        var exec = CreateExecution(isDebug: true);
        Assert.True(exec.IsDebug);
    }

    [Fact]
    public void NewExecution_WithIsDebugFalse_ShouldNotSetDebugFlag()
    {
        var exec = CreateExecution(isDebug: false);
        Assert.False(exec.IsDebug);
    }

    // ─── State transitions ────────────────────────────────────────────────────

    [Fact]
    public void Start_ShouldSetStatusToRunning()
    {
        var exec = CreateExecution();
        exec.Start();
        Assert.Equal(ExecutionStatus.Running, exec.Status);
    }

    [Fact]
    public void Complete_ShouldSetStatusAndOutputs()
    {
        var exec = CreateExecution();
        exec.Start();
        exec.Complete("""{"result":"ok"}""");

        Assert.Equal(ExecutionStatus.Completed, exec.Status);
        Assert.Equal("""{"result":"ok"}""", exec.OutputsJson);
        Assert.NotNull(exec.CompletedAt);
    }

    [Fact]
    public void Fail_ShouldSetStatusAndErrorMessage()
    {
        var exec = CreateExecution();
        exec.Start();
        exec.Fail("Something went wrong");

        Assert.Equal(ExecutionStatus.Failed, exec.Status);
        Assert.Equal("Something went wrong", exec.ErrorMessage);
        Assert.NotNull(exec.CompletedAt);
    }

    [Fact]
    public void Cancel_ShouldSetStatusToCancelled()
    {
        var exec = CreateExecution();
        exec.Start();
        exec.Cancel();

        Assert.Equal(ExecutionStatus.Cancelled, exec.Status);
        Assert.NotNull(exec.CompletedAt);
    }

    [Fact]
    public void Interrupt_ShouldSetInterruptTypeAndNodeKey()
    {
        var exec = CreateExecution();
        exec.Start();
        exec.Interrupt(InterruptType.ManualApproval, "approval_node_1");

        Assert.Equal(ExecutionStatus.Interrupted, exec.Status);
        Assert.Equal(InterruptType.ManualApproval, exec.InterruptType);
        Assert.Equal("approval_node_1", exec.InterruptNodeKey);
    }

    [Fact]
    public void Resume_AfterInterrupt_ShouldClearInterruptFields()
    {
        var exec = CreateExecution();
        exec.Start();
        exec.Interrupt(InterruptType.ManualApproval, "approval_node_1");
        exec.Resume();

        Assert.Equal(ExecutionStatus.Running, exec.Status);
        Assert.Equal(InterruptType.None, exec.InterruptType);
        Assert.Null(exec.InterruptNodeKey);
    }

    [Fact]
    public void BindRuntimeReferences_ShouldSetAppAndReleaseIds()
    {
        var exec = CreateExecution();
        exec.BindRuntimeReferences(appId: 42L, releaseId: 7L, runtimeContextId: 99L);

        Assert.Equal(42L, exec.AppId);
        Assert.Equal(7L, exec.ReleaseId);
        Assert.Equal(99L, exec.RuntimeContextId);
    }

    [Fact]
    public void BindRuntimeReferences_WithNulls_ShouldClearIds()
    {
        var exec = CreateExecution();
        exec.BindRuntimeReferences(appId: 10L, releaseId: 5L, runtimeContextId: 3L);
        exec.BindRuntimeReferences(appId: null, releaseId: null, runtimeContextId: null);

        Assert.Null(exec.AppId);
        Assert.Null(exec.ReleaseId);
        Assert.Null(exec.RuntimeContextId);
    }

    // ─── InputsJson ───────────────────────────────────────────────────────────

    [Fact]
    public void NewExecution_WithInputs_ShouldPreserveInputsJson()
    {
        const string inputs = """{"key":"value"}""";
        var exec = CreateExecution(inputsJson: inputs);
        Assert.Equal(inputs, exec.InputsJson);
    }

    [Fact]
    public void NewExecution_WithNullInputs_ShouldHaveNullInputsJson()
    {
        var exec = CreateExecution(inputsJson: null);
        Assert.Null(exec.InputsJson);
    }

    // ─── WorkflowExecutionCleanup logic ──────────────────────────────────────

    [Fact]
    public void CompletedExecution_CompletedAtShouldBeSet()
    {
        var exec = CreateExecution();
        exec.Start();
        exec.Complete("{}");

        Assert.NotNull(exec.CompletedAt);
        Assert.True(exec.CompletedAt >= exec.StartedAt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsDebug_IsPreservedAfterStateTransitions(bool isDebug)
    {
        var exec = CreateExecution(isDebug: isDebug);
        exec.Start();
        exec.Complete("{}");
        Assert.Equal(isDebug, exec.IsDebug);
    }
}
