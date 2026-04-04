using FluentValidation;
using Atlas.Presentation.Shared.Models;

namespace Atlas.Presentation.Shared.Validators;

public sealed class UserProfileUpdateViewModelValidator : AbstractValidator<UserProfileUpdateViewModel>
{
    public UserProfileUpdateViewModelValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.Email)
            .MaximumLength(256)
            .When(x => x.Email is not null);

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(32)
            .When(x => x.PhoneNumber is not null);
    }
}
