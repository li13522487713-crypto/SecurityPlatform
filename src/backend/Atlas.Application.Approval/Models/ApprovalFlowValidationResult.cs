namespace Atlas.Application.Approval.Models;

public sealed record ApprovalFlowValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ApprovalFlowValidationIssue> Details);

public sealed record ApprovalFlowValidationIssue(
    string Code,
    string Message,
    string Severity,
    string? NodeId = null,
    string? EdgeId = null);
