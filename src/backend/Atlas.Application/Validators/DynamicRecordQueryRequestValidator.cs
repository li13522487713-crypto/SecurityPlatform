using Atlas.Application.DynamicTables.Models;
using Atlas.Application.Resources;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace Atlas.Application.Validators;

public sealed class DynamicRecordQueryRequestValidator : AbstractValidator<DynamicRecordQueryRequest>
{
    public DynamicRecordQueryRequestValidator(IStringLocalizer<Messages> localizer)
    {
        RuleFor(x => x.PageIndex).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Keyword).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.Keyword));
        RuleForEach(x => x.Filters)
            .Must(filter => !string.IsNullOrWhiteSpace(filter.Field))
            .WithMessage(localizer["DynamicRecordFilterFieldRequired"].Value);

        When(x => x.AdvancedQuery != null, () =>
        {
            RuleFor(x => x.AdvancedQuery!.RootGroup).NotNull();
        });
    }
}
