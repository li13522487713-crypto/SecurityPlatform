namespace Atlas.Application.System.Abstractions;

/// <summary>
/// Provides a unified view of cross-entity metadata references for a given dynamic table.
/// Answers: "which forms, pages, and approval flows reference this table?"
/// </summary>
public interface IMetadataLinkQueryService
{
    /// <summary>
    /// Returns all metadata artifacts (forms, pages, approval flow) that reference the specified table key.
    /// </summary>
    Task<EntityReferenceResult> GetEntityReferencesAsync(
        string tableKey,
        CancellationToken cancellationToken = default);
}

/// <summary>Aggregated reference result for a dynamic table.</summary>
public sealed record EntityReferenceResult(
    string TableKey,
    IReadOnlyList<FormDefinitionRef> FormDefinitions,
    IReadOnlyList<LowCodePageRef> LowCodePages,
    ApprovalFlowRef? BoundApprovalFlow);

public sealed record FormDefinitionRef(long Id, string Name, string? Description, string? Category, string? Status);

public sealed record LowCodePageRef(long Id, string PageKey, string Name, long AppId);

public sealed record ApprovalFlowRef(long Id, string Name, string Status);
