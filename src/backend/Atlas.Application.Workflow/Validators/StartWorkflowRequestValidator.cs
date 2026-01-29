using Atlas.Application.Workflow.Models;
using FluentValidation;

namespace Atlas.Application.Workflow.Validators;

/// <summary>
/// 启动工作流请求验证器
/// </summary>
public class StartWorkflowRequestValidator : AbstractValidator<StartWorkflowRequest>
{
    public StartWorkflowRequestValidator()
    {
        RuleFor(x => x.WorkflowId)
            .NotEmpty()
            .WithMessage("工作流ID不能为空");

        RuleFor(x => x.Version)
            .GreaterThan(0)
            .When(x => x.Version.HasValue)
            .WithMessage("版本号必须大于0");
    }
}
