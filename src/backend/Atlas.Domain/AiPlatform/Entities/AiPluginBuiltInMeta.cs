namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiPluginBuiltInMeta
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Version { get; init; } = "1.0.0";
    public IReadOnlyList<string> Tags { get; init; } = [];
}
