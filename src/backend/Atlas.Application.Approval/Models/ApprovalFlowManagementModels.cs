namespace Atlas.Application.Approval.Models;

public sealed record ApprovalFlowCopyRequest(string? Name);

public sealed record ApprovalFlowImportRequest(
    string Name,
    string DefinitionJson,
    string? Description,
    string? Category,
    string? VisibilityScopeJson,
    bool? IsQuickEntry);

public sealed record ApprovalFlowExportResponse(
    long Id,
    string Name,
    int Version,
    string DefinitionJson,
    string? Description,
    string? Category,
    string? VisibilityScopeJson,
    bool IsQuickEntry,
    DateTimeOffset ExportedAt);

public sealed record ApprovalFlowDifferenceItem(
    string Path,
    string SourceValue,
    string TargetValue,
    string ChangeType);

public sealed record ApprovalFlowCompareResponse(
    long SourceFlowId,
    int SourceVersion,
    int TargetVersion,
    bool IsSame,
    string Summary,
    IReadOnlyList<ApprovalFlowDifferenceItem> Differences);
