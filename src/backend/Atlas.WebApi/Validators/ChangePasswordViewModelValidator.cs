using Atlas.Application.Options;
using Atlas.Application.Resources;
using Atlas.Application.Security;
using Atlas.WebApi.Models;
using FluentValidation;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Atlas.WebApi.Validators;

public sealed class ChangePasswordViewModelValidator : AbstractValidator<ChangePasswordViewModel>
{
    public ChangePasswordViewModelValidator(
        IOptionsMonitor<PasswordPolicyOptions> policyOptions,
        IStringLocalizer<Messages> localizer)
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage(localizer["CurrentPasswordRequired"].Value)
            .MaximumLength(128).WithMessage(localizer["PasswordMaxLength", 128].Value);

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage(localizer["NewPasswordRequired"].Value)
            .MaximumLength(128).WithMessage(localizer["PasswordMaxLength", 128].Value);

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage(localizer["ConfirmPasswordRequired"].Value)
            .MaximumLength(128).WithMessage(localizer["PasswordMaxLength", 128].Value);

        RuleFor(x => x)
            .Must(x => x.NewPassword == x.ConfirmPassword)
            .WithMessage(localizer["ConfirmPasswordMismatch"].Value);

        RuleFor(x => x.NewPassword).Custom((value, context) =>
        {
            var policy = policyOptions.CurrentValue;
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
