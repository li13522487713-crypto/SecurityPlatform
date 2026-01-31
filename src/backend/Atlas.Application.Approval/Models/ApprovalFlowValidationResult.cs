namespace Atlas.Application.Approval.Models;

public sealed record ApprovalFlowValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
