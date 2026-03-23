using FluentValidation;
using Atlas.Application.Models;
using Atlas.Application.Options;
using Atlas.Application.Security;
using Microsoft.Extensions.Options;

namespace Atlas.Application.Validators;

public sealed class AuthTokenRequestValidator : AbstractValidator<AuthTokenRequest>
{
    public AuthTokenRequestValidator(IOptionsMonitor<PasswordPolicyOptions> policyOptions)
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
