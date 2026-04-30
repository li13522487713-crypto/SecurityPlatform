namespace Atlas.Application.Microflows.Models;

public sealed record MicroflowExecutionPlanCacheKey(
    string? ResourceId,
    string SchemaId,
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
        Version ?? string.Empty,
        SchemaVersion ?? string.Empty,
        Mode,
        MetadataVersion ?? string.Empty,
        ConnectorCapabilitiesHash);
}
