using FluentValidation;
using Atlas.Application.Models;

namespace Atlas.Application.Validators;

public sealed class AuthTokenRequestValidator : AbstractValidator<AuthTokenRequest>
{
    public AuthTokenRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}