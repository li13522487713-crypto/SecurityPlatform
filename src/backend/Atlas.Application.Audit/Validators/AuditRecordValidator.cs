using FluentValidation;
using Atlas.Domain.Audit.Entities;

namespace Atlas.Application.Audit.Validators;

public sealed class AuditRecordValidator : AbstractValidator<AuditRecord>
{
    public AuditRecordValidator()
    {
        RuleFor(x => x.Actor).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Action).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Result).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Target).MaximumLength(256);
        RuleFor(x => x.IpAddress).MaximumLength(64);
        RuleFor(x => x.UserAgent).MaximumLength(256);
        RuleFor(x => x.ClientType).MaximumLength(32);
        RuleFor(x => x.ClientPlatform).MaximumLength(32);
        RuleFor(x => x.ClientChannel).MaximumLength(32);
        RuleFor(x => x.ClientAgent).MaximumLength(32);
    }
}
