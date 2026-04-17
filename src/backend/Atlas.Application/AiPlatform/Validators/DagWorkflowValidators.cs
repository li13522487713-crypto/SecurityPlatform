using Atlas.Application.AiPlatform.Models;
using FluentValidation;
using System.Text.Json;

namespace Atlas.Application.AiPlatform.Validators;

internal static class DagWorkflowNameRules
{
    // 与 Coze 上游对齐：src/frontend/packages/workflow/base/src/constants/index.ts
    // WORKFLOW_NAME_REGEX = /^[a-zA-Z][a-zA-Z0-9_]{0,63}$/
    // WORKFLOW_NAME_MAX_LEN = 30（产品上限）
    // 后端用单一正则同时收紧字符集与长度（首字母 + 最多 29 个 ASCII 字符 = 总长度 1..30）
    public const string Regex = "^[A-Za-z][A-Za-z0-9_]{0,29}$";

    public const int MaxLength = 30;

    public const string FormatErrorCode = "DAG_WORKFLOW_NAME_FORMAT";
    public const string LengthErrorCode = "DAG_WORKFLOW_NAME_LENGTH";

    public const string FormatMessage = "工作流名称必须以英文字母开头，仅允许字母、数字和下划线。";
    public const string LengthMessage = "工作流名称最长 30 个字符。";
}

public sealed class DagWorkflowCreateRequestValidator : AbstractValidator<DagWorkflowCreateRequest>
{
    public DagWorkflowCreateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(DagWorkflowNameRules.MaxLength)
                .WithErrorCode(DagWorkflowNameRules.LengthErrorCode)
                .WithMessage(DagWorkflowNameRules.LengthMessage)
            .Matches(DagWorkflowNameRules.Regex)
                .WithErrorCode(DagWorkflowNameRules.FormatErrorCode)
                .WithMessage(DagWorkflowNameRules.FormatMessage);
        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(2000);
    }
}

public sealed class DagWorkflowSaveDraftRequestValidator : AbstractValidator<DagWorkflowSaveDraftRequest>
{
    public DagWorkflowSaveDraftRequestValidator()
    {
        RuleFor(x => x.CanvasJson).NotEmpty();
    }
}

public sealed class DagWorkflowUpdateMetaRequestValidator : AbstractValidator<DagWorkflowUpdateMetaRequest>
{
    public DagWorkflowUpdateMetaRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(DagWorkflowNameRules.MaxLength)
                .WithErrorCode(DagWorkflowNameRules.LengthErrorCode)
                .WithMessage(DagWorkflowNameRules.LengthMessage)
            .Matches(DagWorkflowNameRules.Regex)
                .WithErrorCode(DagWorkflowNameRules.FormatErrorCode)
                .WithMessage(DagWorkflowNameRules.FormatMessage);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}

public sealed class DagWorkflowPublishRequestValidator : AbstractValidator<DagWorkflowPublishRequest>
{
    public DagWorkflowPublishRequestValidator()
    {
        RuleFor(x => x.ChangeLog).MaximumLength(500).When(x => !string.IsNullOrWhiteSpace(x.ChangeLog));
    }
}

public sealed class DagWorkflowRunRequestValidator : AbstractValidator<DagWorkflowRunRequest>
{
    public DagWorkflowRunRequestValidator()
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

public sealed class DagWorkflowNodeDebugRequestValidator : AbstractValidator<DagWorkflowNodeDebugRequest>
{
    public DagWorkflowNodeDebugRequestValidator()
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
