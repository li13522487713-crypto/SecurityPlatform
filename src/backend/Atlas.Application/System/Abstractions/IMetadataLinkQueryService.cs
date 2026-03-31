namespace Atlas.Application.System.Abstractions;

/// <summary>
/// Provides a unified view of cross-entity metadata references for a given dynamic table.
/// </summary>
public interface IMetadataLinkQueryService
{
    Task<EntityReferenceResult> GetEntityReferencesAsync(
        string tableKey,
        CancellationToken cancellationToken = default);
}

public sealed record EntityReferenceResult(
    string TableKey,
    IReadOnlyList<FormDefinitionRef> FormDefinitions,
    IReadOnlyList<LowCodePageRef> LowCodePages,
    ApprovalFlowRef? BoundApprovalFlow,
    IReadOnlyList<DynamicRelationRef>? Relations = null,
    IReadOnlyList<DynamicViewRef>? DynamicViews = null,
    IReadOnlyList<TransformJobRef>? TransformJobs = null);

public sealed record FormDefinitionRef(long Id, string Name, string? Description, string? Category, string? Status);

public sealed record LowCodePageRef(long Id, string PageKey, string Name, long AppId);

public sealed record ApprovalFlowRef(long Id, string Name, string Status);

public sealed record DynamicRelationRef(long Id, string RelatedTableKey, string SourceField, string TargetField);

public sealed record DynamicViewRef(string ViewKey, string Name);

public sealed record TransformJobRef(long Id, string JobKey, string Name, string Status);
