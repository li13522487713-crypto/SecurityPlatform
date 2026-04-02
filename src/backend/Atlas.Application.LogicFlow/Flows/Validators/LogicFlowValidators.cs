using Atlas.Application.LogicFlow.Flows.Models;
using FluentValidation;

namespace Atlas.Application.LogicFlow.Flows.Validators;

public sealed class LogicFlowCreateRequestValidator : AbstractValidator<LogicFlowCreateRequest>
{
    public LogicFlowCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TriggerType).InclusiveBetween(0, 4);
        RuleFor(x => x.MaxRetries)
            .InclusiveBetween(0, 10)
            .When(x => x.MaxRetries.HasValue);
        RuleFor(x => x.TimeoutSeconds)
            .InclusiveBetween(1, 3600)
            .When(x => x.TimeoutSeconds.HasValue);
    }
}

public sealed class LogicFlowUpdateRequestValidator : AbstractValidator<LogicFlowUpdateRequest>
{
    public LogicFlowUpdateRequestValidator()
    {
        Include(new LogicFlowCreateRequestValidator());
    }
}
