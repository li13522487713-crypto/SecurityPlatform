namespace Atlas.Application.Approval.Models;

public sealed record ApprovalFlowVersionListItem(
    string Id,
    string DefinitionId,
    int SnapshotVersion,
    string Name,
    string? Description,
    string? Category,
    long CreatedBy,
    DateTimeOffset CreatedAt);

public sealed record ApprovalFlowVersionDetail(
    string Id,
    string DefinitionId,
    int SnapshotVersion,
    string Name,
    string? Description,
    string? Category,
    string DefinitionJson,
    string? VisibilityScopeJson,
    long CreatedBy,
    DateTimeOffset CreatedAt);
