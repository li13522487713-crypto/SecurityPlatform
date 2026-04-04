using FluentValidation;
using Atlas.Application.Options;
using Atlas.Application.Security;
using Atlas.Presentation.Shared.Models;
using Microsoft.Extensions.Options;

namespace Atlas.Presentation.Shared.Validators;

public sealed class AuthTokenViewModelValidator : AbstractValidator<AuthTokenViewModel>
{
    public AuthTokenViewModelValidator(IOptionsMonitor<PasswordPolicyOptions> policyOptions)
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Password).Custom((value, context) =>
        {
            if (!PasswordPolicy.IsCompliant(value, policyOptions.CurrentValue, out var message))
            {
                context.AddFailure(message);
            }
        });
    }
}
