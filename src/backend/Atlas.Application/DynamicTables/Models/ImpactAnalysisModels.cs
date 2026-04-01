namespace Atlas.Application.DynamicTables.Models;

public sealed record DynamicImpactAnalysisResult(
    string TableKey,
    IReadOnlyList<ImpactedResource> AffectedPages,
    IReadOnlyList<ImpactedResource> AffectedForms,
    IReadOnlyList<ImpactedResource> AffectedFlows,
    IReadOnlyList<ImpactedResource> AffectedAgents,
    string RiskLevel,
    int TotalAffectedCount);

public sealed record ImpactedResource(
    string ResourceType,
    string ResourceId,
    string ResourceName,
    string? ResourcePath);
