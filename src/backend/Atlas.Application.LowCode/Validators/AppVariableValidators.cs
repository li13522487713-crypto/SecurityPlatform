using FluentValidation;
using Atlas.Application.LowCode.Models;

namespace Atlas.Application.LowCode.Validators;

public sealed class AppVariableCreateRequestValidator : AbstractValidator<AppVariableCreateRequest>
{
    private static readonly HashSet<string> AllowedScopes = new(StringComparer.Ordinal) { "app", "system" };
    private static readonly HashSet<string> AllowedValueTypes = new(StringComparer.Ordinal)
    {
        "string", "number", "boolean", "date", "array", "object", "file", "image", "any"
    };

    public AppVariableCreateRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Matches(LowCodeCodeRule.Pattern);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Scope).NotEmpty()
            .Must(s => AllowedScopes.Contains(s))
            .WithMessage("scope 仅允许 app / system（page 级变量内联在 PageSchema 中）");
        RuleFor(x => x.ValueType).NotEmpty()
            .Must(t => AllowedValueTypes.Contains(t))
            .WithMessage("valueType 必须为 9 类之一：string/number/boolean/date/array/object/file/image/any");
        RuleFor(x => x.DefaultValueJson).NotEmpty().MaximumLength(100_000);
        RuleFor(x => x.ValidationJson).MaximumLength(100_000);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class AppVariableUpdateRequestValidator : AbstractValidator<AppVariableUpdateRequest>
{
    private static readonly HashSet<string> AllowedValueTypes = new(StringComparer.Ordinal)
    {
        "string", "number", "boolean", "date", "array", "object", "file", "image", "any"
    };

    public AppVariableUpdateRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ValueType).NotEmpty()
            .Must(t => AllowedValueTypes.Contains(t));
        RuleFor(x => x.DefaultValueJson).NotEmpty().MaximumLength(100_000);
        RuleFor(x => x.ValidationJson).MaximumLength(100_000);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}
