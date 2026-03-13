using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class AiRecentEditCreateRequestValidator : AbstractValidator<AiRecentEditCreateRequest>
{
    public AiRecentEditCreateRequestValidator()
    {
        RuleFor(x => x.ResourceType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ResourceId).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Path).NotEmpty().MaximumLength(512);
    }
}
