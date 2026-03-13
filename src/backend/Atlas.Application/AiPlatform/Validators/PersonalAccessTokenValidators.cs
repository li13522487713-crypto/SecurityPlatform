using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class PersonalAccessTokenCreateRequestValidator : AbstractValidator<PersonalAccessTokenCreateRequest>
{
    public PersonalAccessTokenCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Scopes).NotNull();
        RuleForEach(x => x.Scopes).NotEmpty().MaximumLength(64);
    }
}

public sealed class PersonalAccessTokenUpdateRequestValidator : AbstractValidator<PersonalAccessTokenUpdateRequest>
{
    public PersonalAccessTokenUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Scopes).NotNull();
        RuleForEach(x => x.Scopes).NotEmpty().MaximumLength(64);
    }
}
