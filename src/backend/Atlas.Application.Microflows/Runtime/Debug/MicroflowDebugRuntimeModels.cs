using System.Collections.Concurrent;

namespace Atlas.Application.Microflows.Runtime.Debug;

public sealed record MicroflowDebugSession
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string MicroflowId { get; init; } = string.Empty;

    public string? TenantId { get; init; }

    public string? WorkspaceId { get; init; }

    public string? AppId { get; init; }

    public string? CreatedBy { get; init; }

    public string? RunId { get; init; }

    /// <summary>绑定到引擎主循环 trace/run（通常为 RequestContext.TraceId）。</summary>
    public string? BoundEngineRunId { get; init; }

    public string? CurrentNodeObjectId { get; init; }

    /// <summary>beforeNode / afterNode</summary>
    public string? PausePhase { get; init; }

    public string? PausedIncomingFlowId { get; init; }

    public string Status { get; init; } = "created";

    public string LastCommand { get; init; } = DebugCommandKind.Pause;

    public string? RunToNodeObjectId { get; init; }

    public string? RunToCursorFlowId { get; init; }

    public MicroflowDebugSafePointSnapshot? CurrentSafePoint { get; init; }

    public MicroflowDebugStepAnchor? StepAnchor { get; init; }

    public IReadOnlyList<BreakpointDescriptor> Breakpoints { get; init; } = Array.Empty<BreakpointDescriptor>();

    public IReadOnlyList<ConditionalBreakpointDescriptor> ConditionalBreakpoints { get; init; } = Array.Empty<ConditionalBreakpointDescriptor>();

    public IReadOnlyList<DebugVariableSnapshot> Variables { get; init; } = Array.Empty<DebugVariableSnapshot>();

    public IReadOnlyList<DebugCallStackFrame> CallStack { get; init; } = Array.Empty<DebugCallStackFrame>();

    public IReadOnlyList<DebugBranchFrame> BranchFrames { get; init; } = Array.Empty<DebugBranchFrame>();

    public IReadOnlyList<DebugTraceEvent> Trace { get; init; } = Array.Empty<DebugTraceEvent>();

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; init; } = DateTimeOffset.UtcNow.AddMinutes(30);

    public string State => Status;

    public IReadOnlyList<string> AvailableCommands => ResolveAvailableCommands(Status, CurrentSafePoint);

    public DateTimeOffset LastUpdatedAt => UpdatedAt;

    private static IReadOnlyList<string> ResolveAvailableCommands(string status, MicroflowDebugSafePointSnapshot? safePoint)
    {
      if (string.Equals(status, MicroflowDebugSessionLifecycle.Cancelled, StringComparison.OrdinalIgnoreCase))
      {
        return Array.Empty<string>();
      }

      if (safePoint is null)
      {
        return new[]
        {
          DebugCommandKind.Pause,
          DebugCommandKind.Stop
        };
      }

      return new[]
      {
        DebugCommandKind.Continue,
        DebugCommandKind.StepOver,
        DebugCommandKind.StepInto,
        DebugCommandKind.StepOut,
        DebugCommandKind.RunToNode,
        DebugCommandKind.RunToCursor,
        DebugCommandKind.Pause,
        DebugCommandKind.Stop
      };
    }
}

/// <summary>
/// DebugSessionStore：调试会话抽象（内存实现见 <see cref="InMemoryDebugSessionStore"/>）。
/// </summary>
public interface IDebugSessionStore
{
    /// <summary>当前存活会话数量（用于宿主层并发上限）。</summary>
    int SessionCount { get; }

    MicroflowDebugSession Create(string microflowId);

    MicroflowDebugSession Create(string microflowId, MicroflowDebugSessionOwner owner);

    MicroflowDebugSession Upsert(MicroflowDebugSession session);

    MicroflowDebugSession? Get(string sessionId);

    MicroflowDebugSession? UpdateStatus(string sessionId, string command);

    IReadOnlyList<MicroflowDebugSession> ListExpired(DateTimeOffset now);

    bool Remove(string sessionId);

    bool Delete(string sessionId);
}

public sealed class InMemoryDebugSessionStore : IDebugSessionStore
{
    private readonly ConcurrentDictionary<string, MicroflowDebugSession> _sessions = new(StringComparer.Ordinal);

    public int SessionCount => _sessions.Count;

    public MicroflowDebugSession Create(string microflowId)
        => Upsert(new MicroflowDebugSession { MicroflowId = microflowId, Status = "created" });

    public MicroflowDebugSession Create(string microflowId, MicroflowDebugSessionOwner owner)
        => Upsert(new MicroflowDebugSession
        {
            MicroflowId = microflowId,
            TenantId = owner.TenantId,
            WorkspaceId = owner.WorkspaceId,
            AppId = owner.AppId,
            CreatedBy = owner.UserId,
            Status = MicroflowDebugSessionLifecycle.Created
        });

    public MicroflowDebugSession Upsert(MicroflowDebugSession session)
    {
        var next = session with { UpdatedAt = DateTimeOffset.UtcNow };
        _sessions[next.Id] = next;
        return next;
    }

    public MicroflowDebugSession? Get(string sessionId)
        => _sessions.TryGetValue(sessionId, out var session) ? session : null;

    public MicroflowDebugSession? UpdateStatus(string sessionId, string command)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return null;
        }

        var normalized = DebugCommandKind.Normalize(command);
        var status = normalized switch
        {
            DebugCommandKind.Cancel or DebugCommandKind.Stop => MicroflowDebugSessionLifecycle.Cancelled,
            DebugCommandKind.Pause => MicroflowDebugSessionLifecycle.Pausing,
            DebugCommandKind.Continue or DebugCommandKind.RunToNode or DebugCommandKind.RunToCursor => MicroflowDebugSessionLifecycle.Running,
            DebugCommandKind.StepOver or DebugCommandKind.StepInto or DebugCommandKind.StepOut => MicroflowDebugSessionLifecycle.Stepping,
            _ => session.Status
        };
        return Upsert(session with { Status = status, LastCommand = normalized });
    }

    public IReadOnlyList<MicroflowDebugSession> ListExpired(DateTimeOffset now)
        => _sessions.Values
            .Where(session => session.ExpiresAt <= now)
            .OrderBy(session => session.ExpiresAt)
            .ToArray();

    public bool Remove(string sessionId)
        => _sessions.TryRemove(sessionId, out _);

    public bool Delete(string sessionId)
        => Remove(sessionId);
}

public sealed class DebugSessionSweeper
{
    private readonly IDebugSessionStore _store;

    public DebugSessionSweeper(IDebugSessionStore store)
    {
        _store = store;
    }

    public int SweepExpired(DateTimeOffset now)
    {
        var removed = 0;
        foreach (var session in _store.ListExpired(now))
        {
            if (_store.Remove(session.Id))
            {
                removed++;
            }
        }

        return removed;
    }
}

public sealed record DebugCommand
{
    public string Command { get; init; } = "continue";

    public string? TargetNodeObjectId { get; init; }

    public string? TargetFlowId { get; init; }

    public BreakpointDescriptor? Breakpoint { get; init; }

    public ConditionalBreakpointDescriptor? ConditionalBreakpoint { get; init; }
}

public sealed record DebugVariableSnapshot
{
    public string Name { get; init; } = string.Empty;

    public string Type { get; init; } = "unknown";

    public string? ValuePreview { get; init; }

    public bool RedactionApplied { get; init; }

    public string ScopeKind { get; init; } = "local";

    public string? ObjectId { get; init; }

    public string? FlowId { get; init; }

    public string? BranchId { get; init; }

    public string? RawValueJson { get; init; }

    public static DebugVariableSnapshot Redact(string name, string type, string? value)
        => new()
        {
            Name = name,
            Type = type,
            ValuePreview = string.IsNullOrWhiteSpace(value) ? value : "***",
            RedactionApplied = true
        };
}

public sealed record DebugWatchExpression
{
    public string Expression { get; init; } = string.Empty;

    public string? Type { get; init; }

    public string? ValuePreview { get; init; }

    public string? Error { get; init; }

    public int DurationMs { get; init; }
}

public sealed record MicroflowDebugSessionOwner
{
    public string? TenantId { get; init; }

    public string? WorkspaceId { get; init; }

    public string? AppId { get; init; }

    public string? UserId { get; init; }
}

public sealed record MicroflowDebugSafePointSnapshot
{
    public string NodeObjectId { get; init; } = string.Empty;

    public string NodeKind { get; init; } = string.Empty;

    public string Phase { get; init; } = string.Empty;

    public string? IncomingFlowId { get; init; }

    public string? OutgoingFlowId { get; init; }

    public string? BranchId { get; init; }

    public string? SplitInstanceId { get; init; }

    public string? LoopIterationId { get; init; }

    public int? LoopIterationIndex { get; init; }

    public string? CallStackFrameId { get; init; }

    public int CallDepth { get; init; }

    public string SemanticKind { get; init; } = string.Empty;

    public DateTimeOffset ArrivedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record MicroflowDebugStepAnchor
{
    public string? RunId { get; init; }

    public string NodeObjectId { get; init; } = string.Empty;

    public string Phase { get; init; } = string.Empty;

    public int CallDepth { get; init; }

    public string SemanticKind { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record DebugCallStackFrame
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string MicroflowId { get; init; } = string.Empty;

    public string? ParentRunId { get; init; }

    public string RunId { get; init; } = string.Empty;

    public string? CallerObjectId { get; init; }

    public string? CallerActionId { get; init; }

    public int Depth { get; init; }

    public string Status { get; init; } = "active";
}

public sealed record DebugBranchFrame
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string? SplitInstanceId { get; init; }

    public string? BranchId { get; init; }

    public string? ParentBranchId { get; init; }

    public string Status { get; init; } = "active";

    public string? CurrentNodeObjectId { get; init; }
}

public sealed record DebugTraceEvent
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Kind { get; init; } = "safePoint";

    public string Message { get; init; } = string.Empty;

    public string? RunId { get; init; }

    public string? NodeObjectId { get; init; }

    public string? FlowId { get; init; }

    public string? BranchId { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public static class DebugCommandKind
{
    public const string Continue = "continue";
    public const string Pause = "pause";
    public const string StepOver = "stepOver";
    public const string StepInto = "stepInto";
    public const string StepOut = "stepOut";
    public const string RunToNode = "runToNode";
    public const string RunToCursor = "runToCursor";
    public const string Cancel = "cancel";
    public const string Stop = "stop";

    public static string Normalize(string? command)
        => command switch
        {
            Continue or Pause or StepOver or StepInto or StepOut or RunToNode or RunToCursor or Cancel or Stop => command!,
            _ => Pause
        };
}

/// <summary>
/// Step-debug 会话生命周期关键字（verify-microflow-step-debug）：created, starting, running, pausing, paused, stepping, waitingAtJoin, completed, failed, cancelled, timedOut, expired。
/// </summary>
public static class MicroflowDebugSessionLifecycle
{
    public const string Created = "created";

    public const string Starting = "starting";

    public const string Running = "running";

    public const string Pausing = "pausing";

    public const string Paused = "paused";

    public const string Stepping = "stepping";

    public const string WaitingAtJoin = "waitingAtJoin";

    public const string Completed = "completed";

    public const string Failed = "failed";

    public const string Cancelled = "cancelled";

    public const string TimedOut = "timedOut";

    public const string Expired = "expired";
}

/// <summary>节点断点（画布元素）。</summary>
public sealed record BreakpointDescriptor(
    string Id,
    string MicroflowObjectId,
    BreakpointScope Scope,
    bool Stale)
{
    public bool Enabled { get; init; } = true;

    public int HitCount { get; init; }

    public int? HitTarget { get; init; }

    public BreakpointSuspendPolicy SuspendPolicy { get; init; } = BreakpointSuspendPolicy.All;
}

/// <summary>条件断点 / hit count / logpoint。</summary>
public sealed record ConditionalBreakpointDescriptor(
    string Id,
    string MicroflowObjectId,
    string ConditionExpression,
    int HitTarget,
    BreakpointSuspendPolicy SuspendPolicy,
    bool LogOnly,
    bool Stale)
{
    public bool Enabled { get; init; } = true;

    public int HitCount { get; init; }

    public BreakpointScope Scope { get; init; } = BreakpointScope.Node;
}

public enum BreakpointScope
{
    Node = 0,

    Flow = 1,

    Expression = 2,

    ErrorHandler = 3,

    GatewayBranch = 4
}

public enum BreakpointSuspendPolicy
{
    All = 0,

    Thread = 1,

    BranchOnly = 2
}
