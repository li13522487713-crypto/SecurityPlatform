using FluentValidation;
using Microsoft.Extensions.Options;
using Atlas.Application.Identity.Models;
using Atlas.Application.Options;

namespace Atlas.Application.Identity.Validators;

public sealed class UserCreateRequestValidator : AbstractValidator<UserCreateRequest>
{
    public UserCreateRequestValidator(IOptions<PasswordPolicyOptions> policyOptions)
    {
        var policy = policyOptions.Value;

        RuleFor(x => x.Username)
            .NotEmpty()
            .MaximumLength(64)
            .Matches(@"^\S+$").WithMessage("用户名不能包含空白字符。")
            .Must(username => !long.TryParse(username, out _)).WithMessage("用户名不能为纯数字。");
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Email).MaximumLength(256).When(x => x.Email is not null);
        RuleFor(x => x.PhoneNumber).MaximumLength(32).When(x => x.PhoneNumber is not null);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(policy.MinLength);

        if (policy.RequireUppercase)
        {
            RuleFor(x => x.Password).Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.");
        }

        if (policy.RequireLowercase)
        {
            RuleFor(x => x.Password).Matches("[a-z]").WithMessage("Password must contain a lowercase letter.");
        }

        if (policy.RequireDigit)
        {
            RuleFor(x => x.Password).Matches("[0-9]").WithMessage("Password must contain a digit.");
        }

        if (policy.RequireNonAlphanumeric)
        {
            RuleFor(x => x.Password).Matches("[^a-zA-Z0-9]").WithMessage("Password must contain a non-alphanumeric character.");
        }
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
