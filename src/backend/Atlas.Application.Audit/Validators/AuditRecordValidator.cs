using FluentValidation;
using Atlas.Domain.Audit.Entities;

namespace Atlas.Application.Audit.Validators;

public sealed class AuditRecordValidator : AbstractValidator<AuditRecord>
{
    public AuditRecordValidator()
    {
        RuleFor(x => x.Action).NotEmpty().MaximumLength(256);
    }
}