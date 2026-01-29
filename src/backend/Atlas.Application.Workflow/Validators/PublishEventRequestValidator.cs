using Atlas.Application.Workflow.Models;
using FluentValidation;

namespace Atlas.Application.Workflow.Validators;

/// <summary>
/// 发布事件请求验证器
/// </summary>
public class PublishEventRequestValidator : AbstractValidator<PublishEventRequest>
{
    public PublishEventRequestValidator()
    {
        RuleFor(x => x.EventName)
            .NotEmpty()
            .WithMessage("事件名称不能为空");

        RuleFor(x => x.EventKey)
            .NotEmpty()
            .WithMessage("事件键不能为空");
    }
}
