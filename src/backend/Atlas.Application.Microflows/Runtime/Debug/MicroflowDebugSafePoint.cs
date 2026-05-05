namespace Atlas.Application.Microflows.Runtime.Debug;

/// <summary>引擎安全暂停相位（与 Mendix step-debug 对齐）。</summary>
public enum MicroflowDebugPausePhase
{
    BeforeNode = 0,

    AfterNode = 1,

    BranchStart = 2,

    BeforeJoin = 3,

    AfterJoin = 4,

    BeforeLoopIteration = 5,

    AfterLoopIteration = 6,

    BeforeCallMicroflow = 7,

    AfterCallMicroflow = 8,

    BeforeErrorHandler = 9,

    AfterErrorHandler = 10,

    BeforeRestRequest = 11,

    AfterRestResponse = 12,

    AfterRestHandled = 13
}

/// <summary>单次安全点快照（可观测：节点、flow、分支、循环与调用栈）。</summary>
public sealed record MicroflowDebugSafePoint(
    MicroflowDebugPausePhase Phase,
    string NodeObjectId,
    string NodeKind,
    string? IncomingFlowId)
{
    public string? OutgoingFlowId { get; init; }

    public string? BranchId { get; init; }

    public string? SplitInstanceId { get; init; }

    public string? LoopIterationId { get; init; }

    public int? LoopIterationIndex { get; init; }

    public string? CallStackFrameId { get; init; }

    public int CallDepth { get; init; }

    public string SemanticKind { get; init; } = string.Empty;
}
