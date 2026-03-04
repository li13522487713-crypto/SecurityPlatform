using Atlas.Application.LowCode.Models;
using FluentValidation;
using System.Text.Json;

namespace Atlas.Application.LowCode.Validators;

file static class LowCodeEnvironmentValidationHelpers
{
    public static bool BeValidVariablesJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(value);
            return doc.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class LowCodeEnvironmentCreateRequestValidator : AbstractValidator<LowCodeEnvironmentCreateRequest>
{
    public LowCodeEnvironmentCreateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("环境名称不能为空")
            .MaximumLength(100).WithMessage("环境名称不能超过100个字符");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("环境编码不能为空")
            .MaximumLength(50).WithMessage("环境编码不能超过50个字符")
            .Matches("^[a-zA-Z][a-zA-Z0-9_-]*$").WithMessage("环境编码格式不正确");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("描述不能超过500个字符");

        RuleFor(x => x.VariablesJson)
            .Must(LowCodeEnvironmentValidationHelpers.BeValidVariablesJson)
            .WithMessage("VariablesJson 必须是 JSON 对象。");
    }
}

public sealed class LowCodeEnvironmentUpdateRequestValidator : AbstractValidator<LowCodeEnvironmentUpdateRequest>
{
    public LowCodeEnvironmentUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("环境名称不能为空")
            .MaximumLength(100).WithMessage("环境名称不能超过100个字符");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("描述不能超过500个字符");

        RuleFor(x => x.VariablesJson)
            .Must(LowCodeEnvironmentValidationHelpers.BeValidVariablesJson)
            .WithMessage("VariablesJson 必须是 JSON 对象。");
    }
}
