using Atlas.Application.AiPlatform.Models;
using FluentValidation;
using System.Text.Json;

namespace Atlas.Application.AiPlatform.Validators;

internal static class WorkflowV2NameRules
{
    // 与 Coze 上游对齐：src/frontend/packages/workflow/base/src/constants/index.ts
    // WORKFLOW_NAME_REGEX = /^[a-zA-Z][a-zA-Z0-9_]{0,63}$/
    // WORKFLOW_NAME_MAX_LEN = 30（产品上限）
    // 后端用单一正则同时收紧字符集与长度（首字母 + 最多 29 个 ASCII 字符 = 总长度 1..30）
    public const string Regex = "^[A-Za-z][A-Za-z0-9_]{0,29}$";

    public const int MaxLength = 30;

    public const string FormatErrorCode = "WORKFLOW_V2_NAME_FORMAT";
    public const string LengthErrorCode = "WORKFLOW_V2_NAME_LENGTH";

    public const string FormatMessage = "工作流名称必须以英文字母开头，仅允许字母、数字和下划线。";
    public const string LengthMessage = "工作流名称最长 30 个字符。";
}

public sealed class WorkflowV2CreateRequestValidator : AbstractValidator<WorkflowV2CreateRequest>
{
    public WorkflowV2CreateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(WorkflowV2NameRules.MaxLength)
                .WithErrorCode(WorkflowV2NameRules.LengthErrorCode)
                .WithMessage(WorkflowV2NameRules.LengthMessage)
            .Matches(WorkflowV2NameRules.Regex)
                .WithErrorCode(WorkflowV2NameRules.FormatErrorCode)
                .WithMessage(WorkflowV2NameRules.FormatMessage);
        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(2000);
    }
}

public sealed class WorkflowV2SaveDraftRequestValidator : AbstractValidator<WorkflowV2SaveDraftRequest>
{
    public WorkflowV2SaveDraftRequestValidator()
    {
        RuleFor(x => x.CanvasJson).NotEmpty();
    }
}

public sealed class WorkflowV2UpdateMetaRequestValidator : AbstractValidator<WorkflowV2UpdateMetaRequest>
{
    public WorkflowV2UpdateMetaRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(WorkflowV2NameRules.MaxLength)
                .WithErrorCode(WorkflowV2NameRules.LengthErrorCode)
                .WithMessage(WorkflowV2NameRules.LengthMessage)
            .Matches(WorkflowV2NameRules.Regex)
                .WithErrorCode(WorkflowV2NameRules.FormatErrorCode)
                .WithMessage(WorkflowV2NameRules.FormatMessage);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}

public sealed class WorkflowV2PublishRequestValidator : AbstractValidator<WorkflowV2PublishRequest>
{
    public WorkflowV2PublishRequestValidator()
    {
        RuleFor(x => x.ChangeLog).MaximumLength(500).When(x => !string.IsNullOrWhiteSpace(x.ChangeLog));
    }
}

public sealed class WorkflowV2RunRequestValidator : AbstractValidator<WorkflowV2RunRequest>
{
    public WorkflowV2RunRequestValidator()
    {
        RuleFor(x => x.Source)
            .Must(source =>
                string.IsNullOrWhiteSpace(source) ||
                string.Equals(source, "published", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source, "draft", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Source 仅支持 published 或 draft。");

        RuleFor(x => x.InputsJson)
            .Must(BeValidJsonObject)
            .When(x => !string.IsNullOrWhiteSpace(x.InputsJson))
            .WithMessage("InputsJson 必须是合法 JSON 对象。");
    }

    private static bool BeValidJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class WorkflowV2NodeDebugRequestValidator : AbstractValidator<WorkflowV2NodeDebugRequest>
{
    public WorkflowV2NodeDebugRequestValidator()
    {
        RuleFor(x => x.NodeKey).NotEmpty().MaximumLength(200);
    }
}

public sealed class WorkflowWorkbenchExecuteRequestValidator : AbstractValidator<WorkflowWorkbenchExecuteRequest>
{
    public WorkflowWorkbenchExecuteRequestValidator()
    {
        RuleFor(x => x.Incident)
            .NotEmpty()
            .MaximumLength(8000);

        RuleFor(x => x.Source)
            .Must(source =>
                string.IsNullOrWhiteSpace(source) ||
                string.Equals(source, "published", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source, "draft", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Source 仅支持 published 或 draft。");
    }
}
