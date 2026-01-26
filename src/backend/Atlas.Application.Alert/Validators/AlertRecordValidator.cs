using FluentValidation;
using Atlas.Domain.Alert.Entities;

namespace Atlas.Application.Alert.Validators;

public sealed class AlertRecordValidator : AbstractValidator<AlertRecord>
{
    public AlertRecordValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
    }
}