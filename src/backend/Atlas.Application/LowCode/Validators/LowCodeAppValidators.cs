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

public sealed class LowCodeAppImportRequestValidator : AbstractValidator<LowCodeAppImportRequest>
{
    private static readonly string[] ConflictStrategies = ["Rename", "Overwrite", "Skip"];

    public LowCodeAppImportRequestValidator()
    {
        RuleFor(x => x.Package)
            .NotNull().WithMessage("导入包不能为空");

        RuleFor(x => x.Package != null ? x.Package.AppKey : null)
            .NotEmpty().WithMessage("导入包应用标识不能为空")
            .MaximumLength(100).WithMessage("导入包应用标识不能超过100个字符")
            .Matches(@"^[a-zA-Z][a-zA-Z0-9_-]*$").WithMessage("导入包应用标识格式非法")
            .When(x => x.Package is not null);

        RuleFor(x => x.Package != null ? x.Package.Name : null)
            .NotEmpty().WithMessage("导入包应用名称不能为空")
            .MaximumLength(200).WithMessage("导入包应用名称不能超过200个字符")
            .When(x => x.Package is not null);

        RuleFor(x => x.ConflictStrategy)
            .NotEmpty().WithMessage("冲突策略不能为空")
            .Must(x => ConflictStrategies.Contains(x, StringComparer.OrdinalIgnoreCase))
            .WithMessage("冲突策略仅支持 Rename/Overwrite/Skip");

        RuleFor(x => x.KeySuffix)
            .MaximumLength(32).WithMessage("后缀长度不能超过32")
            .Matches(@"^[a-zA-Z0-9_-]+$").WithMessage("后缀仅支持字母数字下划线连字符")
            .When(x => !string.IsNullOrWhiteSpace(x.KeySuffix));
    }
}
