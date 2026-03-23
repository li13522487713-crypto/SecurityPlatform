using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class OpenApiProjectCreateRequestValidator : AbstractValidator<OpenApiProjectCreateRequest>
{
    public OpenApiProjectCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Scopes).NotNull();
        RuleForEach(x => x.Scopes).NotEmpty().MaximumLength(64);
    }
}

public sealed class OpenApiProjectUpdateRequestValidator : AbstractValidator<OpenApiProjectUpdateRequest>
{
    public OpenApiProjectUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Scopes).NotNull();
        RuleForEach(x => x.Scopes).NotEmpty().MaximumLength(64);
    }
}

public sealed class OpenApiProjectTokenExchangeRequestValidator : AbstractValidator<OpenApiProjectTokenExchangeRequest>
{
    public OpenApiProjectTokenExchangeRequestValidator()
    {
        RuleFor(x => x.AppId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.AppSecret).NotEmpty().MaximumLength(256);
    }
}
