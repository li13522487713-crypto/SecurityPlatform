using Atlas.Application.LowCode.Models;
using FluentValidation;

namespace Atlas.Application.LowCode.Validators;

public sealed class LowCodeAppCreateRequestValidator : AbstractValidator<LowCodeAppCreateRequest>
{
    public LowCodeAppCreateRequestValidator()
    {
        RuleFor(x => x.AppKey)
            .NotEmpty().WithMessage("应用标识不能为空")
            .MaximumLength(100).WithMessage("应用标识不能超过100个字符")
            .Matches(@"^[a-zA-Z][a-zA-Z0-9_-]*$").WithMessage("应用标识只能包含字母、数字、下划线和连字符，且必须以字母开头");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("应用名称不能为空")
            .MaximumLength(200).WithMessage("应用名称不能超过200个字符");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("描述不能超过1000个字符");

        RuleFor(x => x.Category)
            .MaximumLength(100).WithMessage("分类不能超过100个字符");
    }
}

public sealed class LowCodeAppUpdateRequestValidator : AbstractValidator<LowCodeAppUpdateRequest>
{
    public LowCodeAppUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("应用名称不能为空")
            .MaximumLength(200).WithMessage("应用名称不能超过200个字符");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("描述不能超过1000个字符");

        RuleFor(x => x.Category)
            .MaximumLength(100).WithMessage("分类不能超过100个字符");
    }
}
