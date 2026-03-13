using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class AiWorkspaceUpdateRequestValidator : AbstractValidator<AiWorkspaceUpdateRequest>
{
    public AiWorkspaceUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Theme).NotEmpty().MaximumLength(16);
        RuleFor(x => x.LastVisitedPath).NotEmpty().MaximumLength(256);
    }
}
