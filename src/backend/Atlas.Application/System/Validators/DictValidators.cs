using FluentValidation;
using Atlas.Application.System.Models;
using global::System.Text.RegularExpressions;

namespace Atlas.Application.System.Validators;

/// <summary>
/// 等保2.0：字典编码禁止包含 SQL 关键字及特殊注入字符
/// </summary>
public sealed class DictTypeCreateRequestValidator : AbstractValidator<DictTypeCreateRequest>
{
    private static readonly Regex SafeCodePattern =
        new(@"^[a-z][a-z0-9_]{0,63}$", RegexOptions.Compiled);

    public DictTypeCreateRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(64)
            .Must(c => SafeCodePattern.IsMatch(c))
            .WithMessage("字典编码只允许小写字母、数字和下划线，且必须以字母开头，最长64位。");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Remark).MaximumLength(500).When(x => x.Remark != null);
    }
}

public sealed class DictTypeUpdateRequestValidator : AbstractValidator<DictTypeUpdateRequest>
{
    public DictTypeUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Remark).MaximumLength(500).When(x => x.Remark != null);
    }
}

public sealed class DictDataCreateRequestValidator : AbstractValidator<DictDataCreateRequest>
{
    public DictDataCreateRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Value).NotEmpty().MaximumLength(256);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CssClass).MaximumLength(128).When(x => x.CssClass != null);
        RuleFor(x => x.ListClass).MaximumLength(128).When(x => x.ListClass != null);
    }
}

public sealed class DictDataUpdateRequestValidator : AbstractValidator<DictDataUpdateRequest>
{
    public DictDataUpdateRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Value).NotEmpty().MaximumLength(256);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CssClass).MaximumLength(128).When(x => x.CssClass != null);
        RuleFor(x => x.ListClass).MaximumLength(128).When(x => x.ListClass != null);
    }
}
