namespace Atlas.Infrastructure.Options;

public sealed class CodeExecutionOptions
{
    public string Mode { get; init; } = "Direct";
    public int TimeoutSeconds { get; init; } = 10;
    public int MaxOutputLength { get; init; } = 8000;
    public IReadOnlyList<string> BlockedModules { get; init; } = ["os", "subprocess", "sys"];
}
