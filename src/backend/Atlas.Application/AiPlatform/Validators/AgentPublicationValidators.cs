using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class AgentPublicationPublishRequestValidator : AbstractValidator<AgentPublicationPublishRequest>
{
    public AgentPublicationPublishRequestValidator()
    {
        RuleFor(x => x.ReleaseNote)
            .MaximumLength(2000)
            .When(x => !string.IsNullOrWhiteSpace(x.ReleaseNote));
    }
}

public sealed class AgentPublicationRollbackRequestValidator : AbstractValidator<AgentPublicationRollbackRequest>
{
    public AgentPublicationRollbackRequestValidator()
    {
        RuleFor(x => x.TargetVersion).GreaterThan(0);
        RuleFor(x => x.ReleaseNote)
            .MaximumLength(2000)
            .When(x => !string.IsNullOrWhiteSpace(x.ReleaseNote));
    }
}
