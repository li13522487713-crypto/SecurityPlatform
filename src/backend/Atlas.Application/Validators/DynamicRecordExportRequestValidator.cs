using Atlas.Application.DynamicTables.Models;
using FluentValidation;

namespace Atlas.Application.Validators;

public sealed class DynamicRecordExportRequestValidator : AbstractValidator<DynamicRecordExportRequest>
{
    public DynamicRecordExportRequestValidator()
    {
        RuleFor(x => x.Keyword)
            .MaximumLength(128)
            .When(x => !string.IsNullOrWhiteSpace(x.Keyword));

        RuleFor(x => x.SortBy)
            .MaximumLength(64)
            .When(x => !string.IsNullOrWhiteSpace(x.SortBy));

        RuleForEach(x => x.Fields)
            .MaximumLength(64)
            .When(x => x.Fields is not null);
    }
}
