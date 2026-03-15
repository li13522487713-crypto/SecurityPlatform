using Atlas.Application.Options;
using Atlas.Application.Resources;
using Atlas.Application.Security;
using Atlas.WebApi.Models;
using FluentValidation;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Atlas.WebApi.Validators;

public sealed class RegisterViewModelValidator : AbstractValidator<RegisterViewModel>
{
    public RegisterViewModelValidator(
        IOptions<PasswordPolicyOptions> policyOptions,
        IStringLocalizer<Messages> localizer)
    {
        var policy = policyOptions.Value;

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage(localizer["UsernameRequired"].Value)
            .MaximumLength(64).WithMessage(localizer["UsernameMaxLength", 64].Value)
            .Matches(@"^\S+$").WithMessage(localizer["UsernameNoWhitespace"].Value)
            .Must(username => !long.TryParse(username, out _)).WithMessage(localizer["UsernameNotNumeric"].Value);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(localizer["PasswordRequired"].Value)
            .MaximumLength(128).WithMessage(localizer["PasswordMaxLength", 128].Value);

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage(localizer["ConfirmPasswordRequired"].Value)
            .Equal(x => x.Password).WithMessage(localizer["ConfirmPasswordMismatch"].Value);

        RuleFor(x => x.Password).Custom((value, context) =>
        {
            if (!PasswordPolicy.IsCompliant(
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
