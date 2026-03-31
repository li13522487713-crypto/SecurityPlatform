namespace Atlas.Infrastructure.Options;

public sealed class AgentFrameworkOptions
{
    public bool Enabled { get; init; } = true;

    public string PreferredRuntime { get; init; } = "auto";

    public bool EmitRuntimeSelectionEvent { get; init; } = true;

    public bool PreferSemanticKernelForGroupChat { get; init; } = true;

    public bool PreferMicrosoftAgentFrameworkForWorkflow { get; init; } = true;

    public bool PreferMicrosoftAgentFrameworkForHandoff { get; init; } = true;

    public int GroupChatMaximumRounds { get; init; } = 6;

    public AgentFrameworkPackageCatalogOptions Packages { get; init; } = new();
}

public sealed class AgentFrameworkPackageCatalogOptions
{
    public AgentFrameworkPackageOptions SemanticKernelOrchestration { get; init; } =
        new("Microsoft.SemanticKernel.Agents.Orchestration", "1.74.0-preview");

    public AgentFrameworkPackageOptions MicrosoftAgentFrameworkCore { get; init; } =
        new("Microsoft.Agents.AI", "1.0.0-rc4");

    public AgentFrameworkPackageOptions MicrosoftAgentFrameworkOpenAi { get; init; } =
        new("Microsoft.Agents.AI.OpenAI", "1.0.0-rc4");

    public AgentFrameworkPackageOptions MicrosoftAgentFrameworkWorkflows { get; init; } =
        new("Microsoft.Agents.AI.Workflows", "1.0.0-rc4");
}

public sealed record AgentFrameworkPackageOptions(
    string PackageId,
    string Version);
