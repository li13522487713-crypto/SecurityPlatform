using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Entities;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class AiVariableCreateRequestValidator : AbstractValidator<AiVariableCreateRequest>
{
    public AiVariableCreateRequestValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Value).MaximumLength(20000).When(x => x.Value is not null);
        RuleFor(x => x.ScopeId)
            .GreaterThan(0)
            .When(x => x.Scope is AiVariableScope.Project or AiVariableScope.Bot);
    }
}

public sealed class AiVariableUpdateRequestValidator : AbstractValidator<AiVariableUpdateRequest>
{
    public AiVariableUpdateRequestValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Value).MaximumLength(20000).When(x => x.Value is not null);
        RuleFor(x => x.ScopeId)
            .GreaterThan(0)
            .When(x => x.Scope is AiVariableScope.Project or AiVariableScope.Bot);
    }
}
