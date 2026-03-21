namespace Atlas.Infrastructure.Options;

public sealed class CodeExecutionOptions
{
    public bool Enabled { get; init; } = true;
    /// <summary>
    /// 代码执行模式：
    /// - Sandbox（默认/推荐）：优先使用 Docker 容器隔离，Docker 不可用时回落到宿主机 Python3
    /// - Docker：严格容器沙箱，Docker 不可用时拒绝执行（生产环境强安全推荐）
    /// - Direct：直接在宿主机执行 Python3（高风险，仅限受信任的内网开发/调试环境）
    /// </summary>
    public string Mode { get; init; } = "Sandbox";
    public int TimeoutSeconds { get; init; } = 10;
    public int MaxOutputLength { get; init; } = 8000;
    public IReadOnlyList<string> BlockedModules { get; init; } = ["os", "subprocess", "sys"];
    public DockerSandboxOptions Docker { get; init; } = new();
}

public sealed class DockerSandboxOptions
{
    public string Image { get; init; } = "python:3.12-slim";
    public string MemoryLimit { get; init; } = "64m";
    public double CpuQuota { get; init; } = 0.5;
    public bool AutoDetect { get; init; } = true;
}
