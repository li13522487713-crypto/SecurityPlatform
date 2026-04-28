using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

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
