namespace Atlas.Infrastructure.Options;

public sealed class AgentFrameworkOptions
{
    public bool Enabled { get; init; } = true;

    public bool EmitRuntimeSelectionEvent { get; init; } = true;

    public int GroupChatMaximumRounds { get; init; } = 6;

    public int SingleAgentReducerTargetCount { get; init; } = 20;

    public bool EnableWhiteboardMemory { get; init; } = true;

    public AgentFrameworkPackageCatalogOptions Packages { get; init; } = new();
}

public sealed class AgentFrameworkPackageCatalogOptions
{
    public AgentFrameworkPackageOptions SemanticKernelOrchestration { get; init; } =
        new("Microsoft.SemanticKernel.Agents.Orchestration", "1.74.0-preview");

    public AgentFrameworkPackageOptions SemanticKernelAgentsCore { get; init; } =
        new("Microsoft.SemanticKernel.Agents.Core", "1.74.0");

    public AgentFrameworkPackageOptions SemanticKernelMemory { get; init; } =
        new("Microsoft.SemanticKernel", "1.74.0");
}

public sealed record AgentFrameworkPackageOptions(
    string PackageId,
    string Version);
