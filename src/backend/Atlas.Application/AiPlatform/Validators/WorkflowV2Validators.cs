using Atlas.Application.AiPlatform.Models;
using FluentValidation;
using System.Text.Json;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class WorkflowV2CreateRequestValidator : AbstractValidator<WorkflowV2CreateRequest>
{
    public WorkflowV2CreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => !string.IsNullOrWhiteSpace(x.Description));
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
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(100);
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
