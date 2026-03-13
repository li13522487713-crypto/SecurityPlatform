using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class AiAppCreateRequestValidator : AbstractValidator<AiAppCreateRequest>
{
    public AiAppCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024).When(x => x.Description is not null);
        RuleFor(x => x.Icon).MaximumLength(256).When(x => x.Icon is not null);
        RuleFor(x => x.AgentId).GreaterThan(0).When(x => x.AgentId.HasValue);
        RuleFor(x => x.WorkflowId).GreaterThan(0).When(x => x.WorkflowId.HasValue);
        RuleFor(x => x.PromptTemplateId).GreaterThan(0).When(x => x.PromptTemplateId.HasValue);
    }
}

public sealed class AiAppUpdateRequestValidator : AbstractValidator<AiAppUpdateRequest>
{
    public AiAppUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024).When(x => x.Description is not null);
        RuleFor(x => x.Icon).MaximumLength(256).When(x => x.Icon is not null);
        RuleFor(x => x.AgentId).GreaterThan(0).When(x => x.AgentId.HasValue);
        RuleFor(x => x.WorkflowId).GreaterThan(0).When(x => x.WorkflowId.HasValue);
        RuleFor(x => x.PromptTemplateId).GreaterThan(0).When(x => x.PromptTemplateId.HasValue);
    }
}

public sealed class AiAppPublishRequestValidator : AbstractValidator<AiAppPublishRequest>
{
    public AiAppPublishRequestValidator()
    {
        RuleFor(x => x.ReleaseNote).MaximumLength(2000).When(x => x.ReleaseNote is not null);
    }
}

public sealed class AiAppResourceCopyRequestValidator : AbstractValidator<AiAppResourceCopyRequest>
{
    public AiAppResourceCopyRequestValidator()
    {
        RuleFor(x => x.SourceAppId).GreaterThan(0);
    }
}
