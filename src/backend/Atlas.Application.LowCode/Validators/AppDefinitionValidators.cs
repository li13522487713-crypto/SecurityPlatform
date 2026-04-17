using FluentValidation;
using Atlas.Application.LowCode.Models;

namespace Atlas.Application.LowCode.Validators;

/// <summary>应用 code 通用规则：1-128 字符，仅 [a-zA-Z0-9_-]。</summary>
internal static class LowCodeCodeRule
{
    /// <summary>code 字符正则（避免与路由 / 文件名冲突）。</summary>
    public const string Pattern = "^[a-zA-Z][a-zA-Z0-9_-]{0,127}$";
}

public sealed class AppDefinitionCreateRequestValidator : AbstractValidator<AppDefinitionCreateRequest>
{
    public AppDefinitionCreateRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Matches(LowCodeCodeRule.Pattern)
            .WithMessage("应用编码必须以字母开头，仅允许字母、数字、下划线、短横线，长度 1-128");
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.TargetTypes).NotEmpty().MaximumLength(64)
            .Must(BeValidTargetTypes)
            .WithMessage("targetTypes 必须为 web / mini_program / hybrid 的逗号分隔组合，且至少包含 web");
        RuleFor(x => x.DefaultLocale).MaximumLength(16);
    }

    private static bool BeValidTargetTypes(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "web", "mini_program", "hybrid" };
        foreach (var p in parts)
        {
            if (!allowed.Contains(p)) return false;
        }
        // 至少包含 web（M01 阶段强约束，M15 起允许更灵活的多端组合）
        return Array.Exists(parts, p => string.Equals(p, "web", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class AppDefinitionUpdateRequestValidator : AbstractValidator<AppDefinitionUpdateRequest>
{
    public AppDefinitionUpdateRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.TargetTypes).NotEmpty().MaximumLength(64);
        RuleFor(x => x.DefaultLocale).NotEmpty().MaximumLength(16);
    }
}

public sealed class AppDraftReplaceRequestValidator : AbstractValidator<AppDraftReplaceRequest>
{
    public AppDraftReplaceRequestValidator()
    {
        RuleFor(x => x.SchemaJson).NotEmpty().MaximumLength(2_000_000)
            .WithMessage("schemaJson 不可为空，且单次提交不得超过 2MB（如需更大请走分片上传或拆分页面）");
    }
}

public sealed class AppDraftAutoSaveRequestValidator : AbstractValidator<AppDraftAutoSaveRequest>
{
    public AppDraftAutoSaveRequestValidator()
    {
        RuleFor(x => x.SchemaJson).NotEmpty().MaximumLength(2_000_000);
    }
}

public sealed class AppVersionSnapshotRequestValidator : AbstractValidator<AppVersionSnapshotRequest>
{
    public AppVersionSnapshotRequestValidator()
    {
        RuleFor(x => x.VersionLabel).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Note).MaximumLength(2000);
        RuleFor(x => x.ResourceSnapshotJson).MaximumLength(1_000_000);
    }
}
