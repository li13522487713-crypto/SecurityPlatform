namespace Atlas.Application.Microflows.Models;

public sealed record MicroflowExecutionPlanCacheKey(
    string? ResourceId,
    string SchemaId,
    string SchemaHash,
    string? Version,
    string? SchemaVersion,
    string Mode,
    string? MetadataVersion,
    string ConnectorCapabilitiesHash)
{
    public string StableKey => string.Join(
        "|",
        ResourceId ?? string.Empty,
        SchemaId,
        SchemaHash,
        Version ?? string.Empty,
        SchemaVersion ?? string.Empty,
        Mode,
        MetadataVersion ?? string.Empty,
        ConnectorCapabilitiesHash);
}
