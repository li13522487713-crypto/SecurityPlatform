namespace Atlas.Infrastructure.Options;

public sealed class CodeExecutionOptions
{
    public bool Enabled { get; init; } = true;
    public string Mode { get; init; } = "Direct";
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
