using Atlas.Application.Approval.Models;

namespace Atlas.Application.Approval.Abstractions;

public interface IApprovalDefinitionSemanticValidator
{
    IReadOnlyList<ApprovalFlowValidationIssue> Validate(string definitionJson);
}
