namespace Atlas.Application.Microflows.Runtime.Debug;

/// <summary>
/// 协作式调试闸：引擎在安全点调用 <see cref="WaitAtSafePointAsync"/>，客户端通过调试 API 下发命令后协调器放行。
/// </summary>
public interface IMicroflowDebugCoordinator
{
    Task WaitAtSafePointAsync(
        string? debugSessionId,
        string engineRunId,
        MicroflowDebugSafePoint point,
        CancellationToken cancellationToken);

    Task WaitAtSafePointAsync(
        string? debugSessionId,
        string engineRunId,
        MicroflowDebugSafePoint point,
        MicroflowDebugRuntimeSnapshot snapshot,
        CancellationToken cancellationToken);

    /// <summary>对应 POST .../debug-sessions/{id}/commands（continue/stepOver/...）。</summary>
    void ReleaseOnePause(string debugSessionId);

    MicroflowDebugSession? ApplyCommand(string debugSessionId, DebugCommand command);

    /// <summary>会话删除时释放等待并移除本地闸。</summary>
    void RemoveSession(string debugSessionId);
}

public sealed record MicroflowDebugRuntimeSnapshot
{
    public string? ResourceId { get; init; }

    public string? ParentRunId { get; init; }

    public string RootRunId { get; init; } = string.Empty;

    public int CallDepth { get; init; }

    public IReadOnlyList<string> CallStack { get; init; } = Array.Empty<string>();

    public IReadOnlyList<DebugVariableSnapshot> Variables { get; init; } = Array.Empty<DebugVariableSnapshot>();

    public IReadOnlyList<DebugBranchFrame> BranchFrames { get; init; } = Array.Empty<DebugBranchFrame>();
}
