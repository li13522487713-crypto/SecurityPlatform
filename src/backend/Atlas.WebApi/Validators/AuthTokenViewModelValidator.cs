using FluentValidation;
using Atlas.WebApi.Models;

namespace Atlas.WebApi.Validators;

public sealed class AuthTokenViewModelValidator : AbstractValidator<AuthTokenViewModel>
{
    public AuthTokenViewModelValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}