using System.Collections.Concurrent;

namespace Atlas.Application.Microflows.Runtime.Debug;

/// <summary>
/// 协作式 Step Debug 协调器：每个 safe point 都先登记状态，再根据断点/命令语义决定是否暂停。
/// </summary>
public sealed class MicroflowDebugCoordinator : IMicroflowDebugCoordinator
{
    private static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromMinutes(30);
    private readonly IDebugSessionStore _sessions;
    private readonly ConcurrentDictionary<string, SessionGate> _gates = new(StringComparer.Ordinal);

    public MicroflowDebugCoordinator(IDebugSessionStore sessions)
    {
        _sessions = sessions;
    }

    public Task WaitAtSafePointAsync(
        string? debugSessionId,
        string engineRunId,
        MicroflowDebugSafePoint point,
        CancellationToken cancellationToken)
        => WaitAtSafePointAsync(
            debugSessionId,
            engineRunId,
            point,
            new MicroflowDebugRuntimeSnapshot(),
            cancellationToken);

    public async Task WaitAtSafePointAsync(
        string? debugSessionId,
        string engineRunId,
        MicroflowDebugSafePoint point,
        MicroflowDebugRuntimeSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(debugSessionId))
            return;

        var session = _sessions.Get(debugSessionId);
        if (session is null)
            return;

        if (session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _sessions.Upsert(session with { Status = MicroflowDebugSessionLifecycle.Expired });
            ReleaseOnePause(debugSessionId);
            return;
        }

        if (session.Status is MicroflowDebugSessionLifecycle.Cancelled or MicroflowDebugSessionLifecycle.Completed or MicroflowDebugSessionLifecycle.Failed)
        {
            return;
        }

        var gate = _gates.GetOrAdd(debugSessionId, static _ => new SessionGate());
        var safePoint = ToSnapshot(point);
        var trace = AppendTrace(session.Trace, new DebugTraceEvent
        {
            Kind = "safePoint",
            RunId = engineRunId,
            NodeObjectId = point.NodeObjectId,
            FlowId = point.IncomingFlowId,
            BranchId = point.BranchId,
            Message = $"{safePoint.Phase}:{point.NodeKind}:{point.NodeObjectId}"
        });
        var callStack = BuildCallStack(snapshot, engineRunId, point);
        var next = session with
        {
            Status = MicroflowDebugSessionLifecycle.Paused,
            RunId = session.RunId ?? engineRunId,
            BoundEngineRunId = session.BoundEngineRunId ?? engineRunId,
            CurrentNodeObjectId = point.NodeObjectId,
            PausePhase = safePoint.Phase,
            PausedIncomingFlowId = point.IncomingFlowId,
            CurrentSafePoint = safePoint,
            Variables = Redact(snapshot.Variables, point),
            CallStack = callStack,
            BranchFrames = MergeBranchFrames(session.BranchFrames, snapshot.BranchFrames, point),
            Trace = trace
        };

        _sessions.Upsert(next);

        if (!ShouldPause(next, point))
        {
            _sessions.Upsert(next with { Status = MicroflowDebugSessionLifecycle.Running });
            return;
        }

        using var timeoutCts = new CancellationTokenSource(DefaultWaitTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        try
        {
            await gate.WaitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _sessions.Upsert(next with
            {
                Status = MicroflowDebugSessionLifecycle.TimedOut,
                Trace = AppendTrace(next.Trace, new DebugTraceEvent
                {
                    Kind = "timeout",
                    RunId = engineRunId,
                    NodeObjectId = point.NodeObjectId,
                    Message = "Debug safe point wait timed out."
                })
            });
            return;
        }

        var released = _sessions.Get(debugSessionId);
        if (released is not null && released.Status is not MicroflowDebugSessionLifecycle.Cancelled)
        {
            _sessions.Upsert(released with { Status = MicroflowDebugSessionLifecycle.Running });
        }
    }

    public MicroflowDebugSession? ApplyCommand(string debugSessionId, DebugCommand command)
    {
        var session = _sessions.Get(debugSessionId);
        if (session is null)
            return null;

        if (session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            var expired = _sessions.Upsert(session with { Status = MicroflowDebugSessionLifecycle.Expired });
            ReleaseOnePause(debugSessionId);
            return expired;
        }

        var normalized = DebugCommandKind.Normalize(command.Command);
        var next = normalized switch
        {
            DebugCommandKind.Cancel or DebugCommandKind.Stop => session with
            {
                Status = MicroflowDebugSessionLifecycle.Cancelled,
                LastCommand = normalized
            },
            DebugCommandKind.RunToNode => session with
            {
                Status = MicroflowDebugSessionLifecycle.Running,
                LastCommand = normalized,
                RunToNodeObjectId = command.TargetNodeObjectId,
                StepAnchor = null
            },
            DebugCommandKind.RunToCursor => session with
            {
                Status = MicroflowDebugSessionLifecycle.Running,
                LastCommand = normalized,
                RunToCursorFlowId = command.TargetFlowId,
                StepAnchor = null
            },
            DebugCommandKind.Pause => session with
            {
                Status = MicroflowDebugSessionLifecycle.Pausing,
                LastCommand = normalized
            },
            DebugCommandKind.StepOver or DebugCommandKind.StepInto or DebugCommandKind.StepOut => session with
            {
                Status = MicroflowDebugSessionLifecycle.Stepping,
                LastCommand = normalized,
                StepAnchor = CreateStepAnchor(session)
            },
            _ => session with
            {
                Status = MicroflowDebugSessionLifecycle.Running,
                LastCommand = normalized,
                RunToNodeObjectId = null,
                RunToCursorFlowId = null,
                StepAnchor = null
            }
        };

        if (command.Breakpoint is not null)
        {
            next = next with { Breakpoints = UpsertBreakpoint(next.Breakpoints, command.Breakpoint) };
        }

        if (command.ConditionalBreakpoint is not null)
        {
            next = next with { ConditionalBreakpoints = UpsertConditionalBreakpoint(next.ConditionalBreakpoints, command.ConditionalBreakpoint) };
        }

        next = next with
        {
            Trace = AppendTrace(next.Trace, new DebugTraceEvent
            {
                Kind = "command",
                RunId = next.BoundEngineRunId,
                NodeObjectId = next.CurrentNodeObjectId,
                FlowId = next.PausedIncomingFlowId,
                Message = normalized
            })
        };

        var updated = _sessions.Upsert(next);
        if (normalized is not DebugCommandKind.Pause)
        {
            ReleaseOnePause(debugSessionId);
        }

        return updated;
    }

    public void ReleaseOnePause(string debugSessionId)
    {
        if (string.IsNullOrWhiteSpace(debugSessionId))
            return;

        if (_gates.TryGetValue(debugSessionId, out var gate))
        {
            gate.Release();
        }
    }

    public void RemoveSession(string debugSessionId)
    {
        if (string.IsNullOrWhiteSpace(debugSessionId))
            return;

        if (_gates.TryRemove(debugSessionId, out var gate))
        {
            gate.Dispose();
        }
    }

    public MicroflowDebugSession? UpsertBreakpoint(string debugSessionId, BreakpointDescriptor breakpoint)
    {
        var session = _sessions.Get(debugSessionId);
        if (session is null)
        {
            return null;
        }

        return _sessions.Upsert(session with
        {
            Breakpoints = UpsertBreakpoint(session.Breakpoints, breakpoint),
            Trace = AppendTrace(session.Trace, new DebugTraceEvent
            {
                Kind = "breakpoint",
                RunId = session.BoundEngineRunId ?? session.RunId,
                NodeObjectId = breakpoint.MicroflowObjectId,
                Message = $"upsert:{breakpoint.Scope}:{breakpoint.MicroflowObjectId}"
            })
        });
    }

    public MicroflowDebugSession? RemoveBreakpoint(string debugSessionId, string breakpointId)
    {
        var session = _sessions.Get(debugSessionId);
        if (session is null)
        {
            return null;
        }

        var removed = session.Breakpoints.FirstOrDefault(item => string.Equals(item.Id, breakpointId, StringComparison.Ordinal));
        return _sessions.Upsert(session with
        {
            Breakpoints = session.Breakpoints
                .Where(item => !string.Equals(item.Id, breakpointId, StringComparison.Ordinal))
                .ToArray(),
            Trace = AppendTrace(session.Trace, new DebugTraceEvent
            {
                Kind = "breakpoint",
                RunId = session.BoundEngineRunId ?? session.RunId,
                NodeObjectId = removed?.MicroflowObjectId,
                Message = $"remove:{breakpointId}"
            })
        });
    }

    private static bool ShouldPause(MicroflowDebugSession session, MicroflowDebugSafePoint point)
    {
        if (session.Status is MicroflowDebugSessionLifecycle.Cancelled or MicroflowDebugSessionLifecycle.Expired)
            return false;

        if (session.LastCommand is DebugCommandKind.Pause)
            return true;

        if (session.LastCommand is DebugCommandKind.StepInto)
            return ShouldPauseForStepInto(session, point);

        if (session.LastCommand is DebugCommandKind.StepOver)
            return ShouldPauseForStepOver(session, point);

        if (session.LastCommand is DebugCommandKind.StepOut)
            return ShouldPauseForStepOut(session, point);

        if (session.LastCommand is DebugCommandKind.RunToNode
            && string.Equals(session.RunToNodeObjectId, point.NodeObjectId, StringComparison.Ordinal))
            return true;

        if (session.LastCommand is DebugCommandKind.RunToCursor
            && !string.IsNullOrWhiteSpace(session.RunToCursorFlowId)
            && string.Equals(session.RunToCursorFlowId, point.IncomingFlowId, StringComparison.Ordinal))
            return true;

        return session.Breakpoints.Any(item =>
            item.Enabled
            && !item.Stale
            && item.Scope is BreakpointScope.Node
            && string.Equals(item.MicroflowObjectId, point.NodeObjectId, StringComparison.Ordinal));
    }

    private static MicroflowDebugStepAnchor? CreateStepAnchor(MicroflowDebugSession session)
    {
        var safePoint = session.CurrentSafePoint;
        if (safePoint is null)
            return null;

        return new MicroflowDebugStepAnchor
        {
            RunId = session.BoundEngineRunId ?? session.RunId,
            NodeObjectId = safePoint.NodeObjectId,
            Phase = safePoint.Phase,
            CallDepth = safePoint.CallDepth,
            SemanticKind = safePoint.SemanticKind
        };
    }

    private static bool ShouldPauseForStepInto(MicroflowDebugSession session, MicroflowDebugSafePoint point)
    {
        var anchor = session.StepAnchor;
        return anchor is null || !IsSameStepPoint(anchor, point);
    }

    private static bool ShouldPauseForStepOver(MicroflowDebugSession session, MicroflowDebugSafePoint point)
    {
        var anchor = session.StepAnchor;
        if (anchor is null)
            return true;

        if (IsSameStepPoint(anchor, point) || point.CallDepth > anchor.CallDepth)
            return false;

        if (point.CallDepth < anchor.CallDepth)
            return true;

        var phase = ToPhaseName(point.Phase);
        var anchorWasRestInternal = anchor.SemanticKind == "rest" && IsRestInternalPhase(anchor.Phase);
        if (anchorWasRestInternal)
        {
            return string.Equals(anchor.NodeObjectId, point.NodeObjectId, StringComparison.Ordinal)
                && point.SemanticKind == "rest"
                && (phase == "afterRestHandled" || phase == "afterNode");
        }

        if (point.SemanticKind == "rest" && IsRestInternalPhase(phase))
            return false;

        if (anchor.Phase == "beforeCallMicroflow")
        {
            return string.Equals(anchor.NodeObjectId, point.NodeObjectId, StringComparison.Ordinal)
                ? phase == "afterCallMicroflow"
                : point.CallDepth == anchor.CallDepth;
        }

        return true;
    }

    private static bool ShouldPauseForStepOut(MicroflowDebugSession session, MicroflowDebugSafePoint point)
    {
        var anchor = session.StepAnchor;
        if (anchor is null)
            return true;

        return point.CallDepth < anchor.CallDepth;
    }

    private static bool IsSameStepPoint(MicroflowDebugStepAnchor anchor, MicroflowDebugSafePoint point)
        => string.Equals(anchor.NodeObjectId, point.NodeObjectId, StringComparison.Ordinal)
            && string.Equals(anchor.Phase, ToPhaseName(point.Phase), StringComparison.Ordinal)
            && anchor.CallDepth == point.CallDepth
            && string.Equals(anchor.SemanticKind, point.SemanticKind, StringComparison.Ordinal);

    private static bool IsRestInternalPhase(string? phase)
        => phase is "beforeRestRequest" or "afterRestResponse" or "afterRestHandled";

    private static MicroflowDebugSafePointSnapshot ToSnapshot(MicroflowDebugSafePoint point)
        => new()
        {
            NodeObjectId = point.NodeObjectId,
            NodeKind = point.NodeKind,
            Phase = point.Phase switch
            {
                MicroflowDebugPausePhase.BeforeNode => "beforeNode",
                MicroflowDebugPausePhase.AfterNode => "afterNode",
                MicroflowDebugPausePhase.BranchStart => "branchStart",
                MicroflowDebugPausePhase.BeforeJoin => "beforeJoin",
                MicroflowDebugPausePhase.AfterJoin => "afterJoin",
                MicroflowDebugPausePhase.BeforeLoopIteration => "beforeLoopIteration",
                MicroflowDebugPausePhase.AfterLoopIteration => "afterLoopIteration",
                MicroflowDebugPausePhase.BeforeCallMicroflow => "beforeCallMicroflow",
                MicroflowDebugPausePhase.AfterCallMicroflow => "afterCallMicroflow",
                MicroflowDebugPausePhase.BeforeErrorHandler => "beforeErrorHandler",
                MicroflowDebugPausePhase.AfterErrorHandler => "afterErrorHandler",
                MicroflowDebugPausePhase.BeforeRestRequest => "beforeRestRequest",
                MicroflowDebugPausePhase.AfterRestResponse => "afterRestResponse",
                MicroflowDebugPausePhase.AfterRestHandled => "afterRestHandled",
                _ => point.Phase.ToString()
            },
            IncomingFlowId = point.IncomingFlowId,
            OutgoingFlowId = point.OutgoingFlowId,
            BranchId = point.BranchId,
            SplitInstanceId = point.SplitInstanceId,
            LoopIterationId = point.LoopIterationId,
            LoopIterationIndex = point.LoopIterationIndex,
            CallStackFrameId = point.CallStackFrameId,
            CallDepth = point.CallDepth,
            SemanticKind = point.SemanticKind
        };

    private static string ToPhaseName(MicroflowDebugPausePhase phase)
        => phase switch
        {
            MicroflowDebugPausePhase.BeforeNode => "beforeNode",
            MicroflowDebugPausePhase.AfterNode => "afterNode",
            MicroflowDebugPausePhase.BranchStart => "branchStart",
            MicroflowDebugPausePhase.BeforeJoin => "beforeJoin",
            MicroflowDebugPausePhase.AfterJoin => "afterJoin",
            MicroflowDebugPausePhase.BeforeLoopIteration => "beforeLoopIteration",
            MicroflowDebugPausePhase.AfterLoopIteration => "afterLoopIteration",
            MicroflowDebugPausePhase.BeforeCallMicroflow => "beforeCallMicroflow",
            MicroflowDebugPausePhase.AfterCallMicroflow => "afterCallMicroflow",
            MicroflowDebugPausePhase.BeforeErrorHandler => "beforeErrorHandler",
            MicroflowDebugPausePhase.AfterErrorHandler => "afterErrorHandler",
            MicroflowDebugPausePhase.BeforeRestRequest => "beforeRestRequest",
            MicroflowDebugPausePhase.AfterRestResponse => "afterRestResponse",
            MicroflowDebugPausePhase.AfterRestHandled => "afterRestHandled",
            _ => phase.ToString()
        };

    private static IReadOnlyList<DebugVariableSnapshot> Redact(IReadOnlyList<DebugVariableSnapshot> variables, MicroflowDebugSafePoint point)
        => variables.Select(variable =>
        {
            var shouldRedact = IsSensitive(variable.Name);
            return variable with
            {
                ObjectId = variable.ObjectId ?? point.NodeObjectId,
                FlowId = variable.FlowId ?? point.IncomingFlowId,
                BranchId = variable.BranchId ?? point.BranchId,
                ValuePreview = shouldRedact ? "***" : variable.ValuePreview,
                RawValueJson = shouldRedact ? null : variable.RawValueJson,
                RedactionApplied = variable.RedactionApplied || shouldRedact
            };
        }).ToArray();

    private static bool IsSensitive(string? name)
        => !string.IsNullOrWhiteSpace(name)
            && (name.Contains("secret", StringComparison.OrdinalIgnoreCase)
                || name.Contains("token", StringComparison.OrdinalIgnoreCase)
                || name.Contains("password", StringComparison.OrdinalIgnoreCase)
                || name.Contains("credential", StringComparison.OrdinalIgnoreCase)
                || name.Contains("authorization", StringComparison.OrdinalIgnoreCase)
                || name.Contains("header", StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<DebugCallStackFrame> BuildCallStack(
        MicroflowDebugRuntimeSnapshot snapshot,
        string engineRunId,
        MicroflowDebugSafePoint point)
    {
        if (snapshot.CallStackFrames.Count == 0)
        {
            return
            [
                new DebugCallStackFrame
                {
                    Id = point.CallStackFrameId ?? engineRunId,
                    MicroflowId = snapshot.ResourceId ?? string.Empty,
                    ParentRunId = snapshot.ParentRunId,
                    RunId = engineRunId,
                    Depth = point.CallDepth
                }
            ];
        }

        return snapshot.CallStackFrames.Select((frame, index) => new DebugCallStackFrame
        {
            Id = index == snapshot.CallStackFrames.Count - 1 && !string.IsNullOrWhiteSpace(point.CallStackFrameId)
                ? point.CallStackFrameId!
                : $"{engineRunId}:{index}",
            MicroflowId = frame.TargetResourceId
                ?? frame.TargetQualifiedName
                ?? snapshot.ResourceId
                ?? string.Empty,
            ParentRunId = snapshot.ParentRunId,
            RunId = engineRunId,
            CallerObjectId = frame.CallerObjectId,
            CallerActionId = frame.CallerActionId,
            Depth = index,
            Status = index == snapshot.CallStackFrames.Count - 1
                ? "active"
                : string.IsNullOrWhiteSpace(frame.Status)
                    ? "parent"
                    : frame.Status
        }).ToArray();
    }

    private static IReadOnlyList<DebugBranchFrame> MergeBranchFrames(
        IReadOnlyList<DebugBranchFrame> existing,
        IReadOnlyList<DebugBranchFrame> incoming,
        MicroflowDebugSafePoint point)
    {
        var merged = existing.Concat(incoming).ToDictionary(item => item.Id, StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(point.BranchId))
        {
            var id = point.BranchId!;
            merged[id] = new DebugBranchFrame
            {
                Id = id,
                BranchId = point.BranchId,
                SplitInstanceId = point.SplitInstanceId,
                Status = "active",
                CurrentNodeObjectId = point.NodeObjectId
            };
        }

        return merged.Values.ToArray();
    }

    private static IReadOnlyList<DebugTraceEvent> AppendTrace(IReadOnlyList<DebugTraceEvent> existing, DebugTraceEvent item)
        => existing.Concat([item]).TakeLast(500).ToArray();

    private static IReadOnlyList<BreakpointDescriptor> UpsertBreakpoint(
        IReadOnlyList<BreakpointDescriptor> breakpoints,
        BreakpointDescriptor breakpoint)
        => breakpoints
            .Where(item => !string.Equals(item.Id, breakpoint.Id, StringComparison.Ordinal))
            .Concat([breakpoint])
            .ToArray();

    private static IReadOnlyList<ConditionalBreakpointDescriptor> UpsertConditionalBreakpoint(
        IReadOnlyList<ConditionalBreakpointDescriptor> breakpoints,
        ConditionalBreakpointDescriptor breakpoint)
        => breakpoints
            .Where(item => !string.Equals(item.Id, breakpoint.Id, StringComparison.Ordinal))
            .Concat([breakpoint])
            .ToArray();

    private sealed class SessionGate : IDisposable
    {
        private readonly SemaphoreSlim _gate = new(0, int.MaxValue);

        public Task WaitAsync(CancellationToken cancellationToken)
            => _gate.WaitAsync(cancellationToken);

        public void Release()
            => _gate.Release();

        public void Dispose()
            => _gate.Dispose();
    }
}
