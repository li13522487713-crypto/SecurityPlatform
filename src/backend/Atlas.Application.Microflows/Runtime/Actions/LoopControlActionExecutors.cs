using System.Diagnostics;
using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Loops;

namespace Atlas.Application.Microflows.Runtime.Actions;

public sealed class BreakActionExecutor : IMicroflowActionExecutor
{
    public string ActionKind => "break";

    public string Category => MicroflowActionRuntimeCategory.RuntimeCommand;

    public string SupportLevel => MicroflowActionSupportLevel.Supported;

    public Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var started = Stopwatch.StartNew();
        if (context.RuntimeExecutionContext.LoopStack.Count == 0)
        {
            return Task.FromResult(Failed(started, RuntimeErrorCode.RuntimeLoopControlOutOfScope, "Break can only be used inside a loop."));
        }

        started.Stop();
        return Task.FromResult(new MicroflowActionExecutionResult
        {
            Status = MicroflowLoopBodyExecutionStatus.Break,
            OutputJson = JsonSerializer.SerializeToElement(new { controlSignal = MicroflowLoopControlSignal.Break }),
            OutputPreview = MicroflowLoopControlSignal.Break,
            ShouldContinueNormalFlow = false,
            ShouldStopRun = false,
            DurationMs = (int)started.ElapsedMilliseconds
        });
    }

    private static MicroflowActionExecutionResult Failed(Stopwatch started, string code, string message)
    {
        started.Stop();
        return new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Failed,
            Error = new MicroflowRuntimeErrorDto { Code = code, Message = message },
            Message = message,
            ShouldContinueNormalFlow = false,
            ShouldStopRun = true,
            DurationMs = (int)started.ElapsedMilliseconds
        };
    }
}

public sealed class ContinueActionExecutor : IMicroflowActionExecutor
{
    public string ActionKind => "continue";

    public string Category => MicroflowActionRuntimeCategory.RuntimeCommand;

    public string SupportLevel => MicroflowActionSupportLevel.Supported;

    public Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var started = Stopwatch.StartNew();
        if (context.RuntimeExecutionContext.LoopStack.Count == 0)
        {
            return Task.FromResult(Failed(started, RuntimeErrorCode.RuntimeLoopControlOutOfScope, "Continue can only be used inside a loop."));
        }

        started.Stop();
        return Task.FromResult(new MicroflowActionExecutionResult
        {
            Status = MicroflowLoopBodyExecutionStatus.Continue,
            OutputJson = JsonSerializer.SerializeToElement(new { controlSignal = MicroflowLoopControlSignal.Continue }),
            OutputPreview = MicroflowLoopControlSignal.Continue,
            ShouldContinueNormalFlow = false,
            ShouldStopRun = false,
            DurationMs = (int)started.ElapsedMilliseconds
        });
    }

    private static MicroflowActionExecutionResult Failed(Stopwatch started, string code, string message)
    {
        started.Stop();
        return new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Failed,
            Error = new MicroflowRuntimeErrorDto { Code = code, Message = message },
            Message = message,
            ShouldContinueNormalFlow = false,
            ShouldStopRun = true,
            DurationMs = (int)started.ElapsedMilliseconds
        };
    }
}
