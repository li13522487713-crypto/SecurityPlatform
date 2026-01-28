using FluentValidation;
using Atlas.Application.Approval.Models;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Application.Approval.Validators;

/// <summary>
/// 审批流运行时操作请求验证器
/// </summary>
public sealed class ApprovalOperationRequestValidator : AbstractValidator<ApprovalOperationRequest>
{
    public ApprovalOperationRequestValidator()
    {
        RuleFor(x => x.OperationType)
            .IsInEnum()
            .WithMessage("操作类型无效");

        RuleFor(x => x.Comment)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Comment))
            .WithMessage("操作说明不能超过500个字符");

        RuleFor(x => x.TargetNodeId)
            .NotEmpty()
            .When(x => x.OperationType == ApprovalOperationType.BackToAnyNode)
            .WithMessage("退回任意节点操作需要指定目标节点ID");

        RuleFor(x => x.TargetAssigneeValue)
            .NotEmpty()
            .When(x => x.OperationType == ApprovalOperationType.Transfer
                || x.OperationType == ApprovalOperationType.ChangeAssignee)
            .WithMessage("转办/变更处理人操作需要指定目标处理人");

        RuleFor(x => x.AdditionalAssigneeValues)
            .NotEmpty()
            .When(x => x.OperationType == ApprovalOperationType.AddAssignee)
            .WithMessage("加签操作需要指定至少一个审批人");
    }
}
