using FluentValidation;
using Microsoft.Extensions.Options;
using Atlas.Application.Identity.Models;
using Atlas.Application.Options;
using Atlas.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Atlas.Application.Identity.Validators;

public sealed class UserCreateRequestValidator : AbstractValidator<UserCreateRequest>
{
    public UserCreateRequestValidator(IOptionsMonitor<PasswordPolicyOptions> policyOptions, IStringLocalizer<Messages> localizer)
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .MaximumLength(64)
            .Matches(@"^\S+$").WithMessage(localizer["UsernameNoWhitespace"].Value)
            .Must(username => !long.TryParse(username, out _)).WithMessage(localizer["UsernameNotNumeric"].Value);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Email).MaximumLength(256).When(x => x.Email is not null);
        RuleFor(x => x.PhoneNumber).MaximumLength(32).When(x => x.PhoneNumber is not null);
        RuleFor(x => x.Password)
            .NotEmpty()
            .MaximumLength(128)
            .Custom((value, context) =>
            {
                var policy = policyOptions.CurrentValue;
                if (!Atlas.Application.Security.PasswordPolicy.IsCompliant(
                        value,
                        policy,
                        (key, args, fallback) =>
                        {
                            var localized = args is { Length: > 0 } ? localizer[key, args] : localizer[key];
                            return localized.ResourceNotFound ? fallback : localized.Value;
                        },
                        out var message))
                {
                    context.AddFailure(message);
                }
            });
    }
}

public sealed class UserUpdateRequestValidator : AbstractValidator<UserUpdateRequest>
{
    public UserUpdateRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Email).MaximumLength(256).When(x => x.Email is not null);
        RuleFor(x => x.PhoneNumber).MaximumLength(32).When(x => x.PhoneNumber is not null);
    }
}
