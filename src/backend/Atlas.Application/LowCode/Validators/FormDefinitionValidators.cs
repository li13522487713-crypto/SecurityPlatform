using Atlas.Application.LowCode.Models;
using FluentValidation;

namespace Atlas.Application.LowCode.Validators;

public sealed class FormDefinitionCreateRequestValidator : AbstractValidator<FormDefinitionCreateRequest>
{
    public FormDefinitionCreateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("表单名称不能为空")
            .MaximumLength(200).WithMessage("表单名称不能超过200个字符");

        RuleFor(x => x.SchemaJson)
            .NotEmpty().WithMessage("表单 Schema 不能为空");

        RuleFor(x => x.Category)
            .MaximumLength(100).WithMessage("分类不能超过100个字符");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("描述不能超过1000个字符");
    }
}

public sealed class FormDefinitionUpdateRequestValidator : AbstractValidator<FormDefinitionUpdateRequest>
{
    public FormDefinitionUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("表单名称不能为空")
            .MaximumLength(200).WithMessage("表单名称不能超过200个字符");

        RuleFor(x => x.SchemaJson)
            .NotEmpty().WithMessage("表单 Schema 不能为空");

        RuleFor(x => x.Category)
            .MaximumLength(100).WithMessage("分类不能超过100个字符");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("描述不能超过1000个字符");
    }
}

public sealed class FormDefinitionSchemaUpdateRequestValidator : AbstractValidator<FormDefinitionSchemaUpdateRequest>
{
    public FormDefinitionSchemaUpdateRequestValidator()
    {
        RuleFor(x => x.SchemaJson)
            .NotEmpty().WithMessage("表单 Schema 不能为空");
    }
}
