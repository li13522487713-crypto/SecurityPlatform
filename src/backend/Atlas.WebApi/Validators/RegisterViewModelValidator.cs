using FluentValidation;
using Atlas.Application.Options;
using Atlas.Application.Security;
using Atlas.WebApi.Models;
using Microsoft.Extensions.Options;

namespace Atlas.WebApi.Validators;

public sealed class RegisterViewModelValidator : AbstractValidator<RegisterViewModel>
{
    public RegisterViewModelValidator(IOptions<PasswordPolicyOptions> policyOptions)
    {
        var policy = policyOptions.Value;

        RuleFor(x => x.Username)
            .NotEmpty()
            .MaximumLength(64)
            .Matches(@"^\S+$").WithMessage("用户名不能包含空白字符。")
            .Must(username => !long.TryParse(username, out _)).WithMessage("用户名不能为纯数字。");
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ConfirmPassword).NotEmpty().Equal(x => x.Password);
        RuleFor(x => x.Password).Custom((value, context) =>
        {
            if (!PasswordPolicy.IsCompliant(value, policy, out var message))
            {
                context.AddFailure(message);
            }
        });
    }
}
