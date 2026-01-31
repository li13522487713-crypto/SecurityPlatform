using Atlas.Application.DynamicTables.Models;
using FluentValidation;

namespace Atlas.Application.Validators;

public sealed class DynamicTableUpdateRequestValidator : AbstractValidator<DynamicTableUpdateRequest>
{
    public DynamicTableUpdateRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Status).NotEmpty();
    }
}
