using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class AiWorkflowCreateRequestValidator : AbstractValidator<AiWorkflowCreateRequest>
{
    public AiWorkflowCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.CanvasJson).NotEmpty();
        RuleFor(x => x.DefinitionJson).NotEmpty();
    }
}

public sealed class AiWorkflowSaveRequestValidator : AbstractValidator<AiWorkflowSaveRequest>
{
    public AiWorkflowSaveRequestValidator()
    {
        RuleFor(x => x.CanvasJson).NotEmpty();
        RuleFor(x => x.DefinitionJson).NotEmpty();
    }
}

public sealed class AiWorkflowMetaUpdateRequestValidator : AbstractValidator<AiWorkflowMetaUpdateRequest>
{
    public AiWorkflowMetaUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}
