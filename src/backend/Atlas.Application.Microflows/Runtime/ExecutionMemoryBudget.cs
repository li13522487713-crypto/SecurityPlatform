namespace Atlas.Application.Microflows.Runtime;

public sealed record ExecutionMemoryBudget
{
    public long MaxContextBytes { get; init; } = 16 * 1024 * 1024;

    public long MaxVariableBytes { get; init; } = 1 * 1024 * 1024;

    public long MaxNodeOutputBytes { get; init; } = 1 * 1024 * 1024;

    public long MaxHttpResponseBytes { get; init; } = 2 * 1024 * 1024;

    public long MaxFileVariableBytes { get; init; } = 256 * 1024;

    public int MaxCollectionItems { get; init; } = 1000;

    public int MaxLoopIterations { get; init; } = 1000;

    public int MaxExecutionDepth { get; init; } = 64;

    public int MaxTraceFrames { get; init; } = 5000;
}
