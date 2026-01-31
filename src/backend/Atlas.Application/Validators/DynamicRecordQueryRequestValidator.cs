using Atlas.Application.DynamicTables.Models;
using FluentValidation;

namespace Atlas.Application.Validators;

public sealed class DynamicRecordQueryRequestValidator : AbstractValidator<DynamicRecordQueryRequest>
{
    public DynamicRecordQueryRequestValidator()
    {
        RuleFor(x => x.PageIndex).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Keyword).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.Keyword));
        RuleForEach(x => x.Filters)
            .Must(filter => !string.IsNullOrWhiteSpace(filter.Field))
            .WithMessage("筛选字段不能为空。");
    }
}
