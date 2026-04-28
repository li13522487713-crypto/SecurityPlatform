using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Runtime.Calls;

public interface IMicroflowCallStackService
{
    MicroflowCallStackEnterResult TryEnter(
        RuntimeExecutionContext parentContext,
        string? targetResourceId,
        string? targetQualifiedName,
        string? callerObjectId,
        string? callerActionId,
        int maxCallDepth,
        bool allowRecursion,
        DateTimeOffset startedAt);

    IDisposable Activate(RuntimeExecutionContext parentContext, MicroflowCallStackFrame frame);

    void Complete(MicroflowCallStackFrame frame, string status, DateTimeOffset endedAt, MicroflowRuntimeErrorDto? error = null);
}

public sealed record MicroflowCallStackEnterResult
{
    public bool Allowed { get; init; }
    public MicroflowCallStackFrame? Frame { get; init; }
    public MicroflowRuntimeErrorDto? Error { get; init; }
    public IReadOnlyList<MicroflowCallDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowCallDiagnostic>();
}

public sealed class MicroflowCallStackService : IMicroflowCallStackService
{
    public MicroflowCallStackEnterResult TryEnter(
        RuntimeExecutionContext parentContext,
        string? targetResourceId,
        string? targetQualifiedName,
        string? callerObjectId,
        string? callerActionId,
        int maxCallDepth,
        bool allowRecursion,
        DateTimeOffset startedAt)
    {
        var depth = parentContext.CallStackFrames.Count + 1;
        var chain = parentContext.CallStackFrames
            .Select(frame => frame.TargetQualifiedName ?? frame.TargetResourceId ?? "unknown")
            .Concat([targetQualifiedName ?? targetResourceId ?? "unknown"])
            .ToArray();

        if (depth > Math.Max(1, maxCallDepth))
        {
            var message = $"CallMicroflow maxCallDepth exceeded: {string.Join(" -> ", chain)}";
            return Denied(
                RuntimeErrorCode.RuntimeCallStackOverflow,
                "CALL_MAX_DEPTH_EXCEEDED",
                message,
                callerObjectId,
                callerActionId,
                MicroflowCallStackFrameStatus.MaxDepthExceeded);
        }

        if (!allowRecursion && !string.IsNullOrWhiteSpace(targetResourceId)
            && parentContext.CallStackFrames.Any(frame =>
                string.Equals(frame.TargetResourceId, targetResourceId, StringComparison.Ordinal)
                || string.Equals(frame.CallerResourceId, targetResourceId, StringComparison.Ordinal)))
        {
            var message = $"CallMicroflow recursion detected: {string.Join(" -> ", chain)}";
            return Denied(
                RuntimeErrorCode.RuntimeCallRecursionDetected,
                "CALL_RECURSION_DETECTED",
                message,
                callerObjectId,
                callerActionId,
                MicroflowCallStackFrameStatus.RecursionDetected);
        }

        if (!allowRecursion && string.Equals(parentContext.ResourceId, targetResourceId, StringComparison.Ordinal))
        {
            var message = $"Direct CallMicroflow recursion detected: {string.Join(" -> ", chain)}";
            return Denied(
                RuntimeErrorCode.RuntimeCallRecursionDetected,
                "CALL_RECURSION_DETECTED",
                message,
                callerObjectId,
                callerActionId,
                MicroflowCallStackFrameStatus.RecursionDetected);
        }

        var frame = new MicroflowCallStackFrame
        {
            ParentFrameId = parentContext.CurrentCallFrame?.FrameId,
            Depth = depth,
            CallerResourceId = parentContext.ResourceId,
            CallerSchemaId = parentContext.SchemaId,
            CallerObjectId = callerObjectId,
            CallerActionId = callerActionId,
            TargetResourceId = targetResourceId,
            TargetQualifiedName = targetQualifiedName,
            StartedAt = startedAt,
            Status = MicroflowCallStackFrameStatus.Entering
        };

        return new MicroflowCallStackEnterResult { Allowed = true, Frame = frame };
    }

    public IDisposable Activate(RuntimeExecutionContext parentContext, MicroflowCallStackFrame frame)
    {
        var previous = parentContext.CurrentCallFrame;
        parentContext.CallStackFrames.Add(frame);
        parentContext.CurrentCallFrame = frame;
        frame.Status = MicroflowCallStackFrameStatus.Running;
        return new Lease(() =>
        {
            parentContext.CurrentCallFrame = previous;
        });
    }

    public void Complete(MicroflowCallStackFrame frame, string status, DateTimeOffset endedAt, MicroflowRuntimeErrorDto? error = null)
    {
        frame.Status = status;
        frame.EndedAt = endedAt;
        frame.DurationMs = Math.Max(0, (int)(endedAt - frame.StartedAt).TotalMilliseconds);
        frame.Error = error;
    }

    private static MicroflowCallStackEnterResult Denied(
        string errorCode,
        string diagnosticCode,
        string message,
        string? objectId,
        string? actionId,
        string frameStatus)
    {
        var diagnostic = new MicroflowCallDiagnostic
        {
            Code = diagnosticCode,
            Severity = "error",
            Message = message
        };
        return new MicroflowCallStackEnterResult
        {
            Allowed = false,
            Frame = new MicroflowCallStackFrame
            {
                CallerObjectId = objectId,
                CallerActionId = actionId,
                Status = frameStatus,
                StartedAt = DateTimeOffset.UtcNow,
                Diagnostics = [diagnostic]
            },
            Error = new MicroflowRuntimeErrorDto
            {
                Code = errorCode,
                Message = message,
                ObjectId = objectId,
                ActionId = actionId
            },
            Diagnostics = [diagnostic]
        };
    }

    private sealed class Lease : IDisposable
    {
        private readonly Action _dispose;
        private bool _disposed;

        public Lease(Action dispose) => _dispose = dispose;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _dispose();
        }
    }
}
