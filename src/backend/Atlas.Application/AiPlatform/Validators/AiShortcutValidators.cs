using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class AiShortcutCommandCreateRequestValidator : AbstractValidator<AiShortcutCommandCreateRequest>
{
    public AiShortcutCommandCreateRequestValidator()
    {
        RuleFor(x => x.CommandKey).NotEmpty().MaximumLength(64);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(64);
        RuleFor(x => x.TargetPath).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Description).MaximumLength(512).When(x => x.Description is not null);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class AiShortcutCommandUpdateRequestValidator : AbstractValidator<AiShortcutCommandUpdateRequest>
{
    public AiShortcutCommandUpdateRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(64);
        RuleFor(x => x.TargetPath).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Description).MaximumLength(512).When(x => x.Description is not null);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class AiBotPopupDismissRequestValidator : AbstractValidator<AiBotPopupDismissRequest>
{
    public AiBotPopupDismissRequestValidator()
    {
        RuleFor(x => x.PopupCode).NotEmpty().MaximumLength(64);
    }
}
