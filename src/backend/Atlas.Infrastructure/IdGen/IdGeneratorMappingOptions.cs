namespace Atlas.Infrastructure.IdGen;

public sealed class IdGeneratorMappingOptions
{
    public string DefaultAppId { get; init; } = "SecurityPlatform";
    public List<IdGeneratorMapping> Mappings { get; init; } = new();
}

public sealed record IdGeneratorMapping(string TenantId, string AppId, int GeneratorId);
