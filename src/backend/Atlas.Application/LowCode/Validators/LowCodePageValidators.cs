using Atlas.Application.LowCode.Models;
using FluentValidation;

namespace Atlas.Application.LowCode.Validators;

public sealed class LowCodePageCreateRequestValidator : AbstractValidator<LowCodePageCreateRequest>
{
    public LowCodePageCreateRequestValidator()
    {
        RuleFor(x => x.PageKey)
            .NotEmpty().WithMessage("页面标识不能为空")
            .MaximumLength(100).WithMessage("页面标识不能超过100个字符")
            .Matches(@"^[a-zA-Z][a-zA-Z0-9_-]*$").WithMessage("页面标识只能包含字母、数字、下划线和连字符");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("页面名称不能为空")
            .MaximumLength(200).WithMessage("页面名称不能超过200个字符");

        RuleFor(x => x.PageType)
            .NotEmpty().WithMessage("页面类型不能为空");

        RuleFor(x => x.SchemaJson)
            .NotEmpty().WithMessage("页面 Schema 不能为空");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("描述不能超过1000个字符");
    }
}

public sealed class LowCodePageUpdateRequestValidator : AbstractValidator<LowCodePageUpdateRequest>
{
    public LowCodePageUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("页面名称不能为空")
            .MaximumLength(200).WithMessage("页面名称不能超过200个字符");

        RuleFor(x => x.PageType)
            .NotEmpty().WithMessage("页面类型不能为空");

        RuleFor(x => x.SchemaJson)
            .NotEmpty().WithMessage("页面 Schema 不能为空");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("描述不能超过1000个字符");
    }
}
