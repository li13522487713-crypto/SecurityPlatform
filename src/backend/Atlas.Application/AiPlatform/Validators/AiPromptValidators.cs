using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class AiPromptTemplateCreateRequestValidator : AbstractValidator<AiPromptTemplateCreateRequest>
{
    public AiPromptTemplateCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024).When(x => x.Description is not null);
        RuleFor(x => x.Category).MaximumLength(64).When(x => x.Category is not null);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(50000);
        RuleForEach(x => x.Tags).MaximumLength(32);
    }
}

public sealed class AiPromptTemplateUpdateRequestValidator : AbstractValidator<AiPromptTemplateUpdateRequest>
{
    public AiPromptTemplateUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024).When(x => x.Description is not null);
        RuleFor(x => x.Category).MaximumLength(64).When(x => x.Category is not null);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(50000);
        RuleForEach(x => x.Tags).MaximumLength(32);
    }
}
