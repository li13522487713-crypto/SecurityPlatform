using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class MultiAgentMemberInputValidator : AbstractValidator<MultiAgentMemberInput>
{
    public MultiAgentMemberInputValidator()
    {
        RuleFor(x => x.AgentId).GreaterThan(0);
        RuleFor(x => x.Alias).MaximumLength(100).When(x => !string.IsNullOrWhiteSpace(x.Alias));
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PromptPrefix).MaximumLength(1000).When(x => !string.IsNullOrWhiteSpace(x.PromptPrefix));
    }
}

public sealed class MultiAgentOrchestrationCreateRequestValidator : AbstractValidator<MultiAgentOrchestrationCreateRequest>
{
    public MultiAgentOrchestrationCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024).When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.Mode).IsInEnum();
        RuleFor(x => x.Members).NotEmpty().Must(x => x.Any(item => item.IsEnabled))
            .WithMessage("至少需要一个启用状态的 Agent 成员。");
        RuleForEach(x => x.Members).SetValidator(new MultiAgentMemberInputValidator());
    }
}

public sealed class MultiAgentOrchestrationUpdateRequestValidator : AbstractValidator<MultiAgentOrchestrationUpdateRequest>
{
    public MultiAgentOrchestrationUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024).When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.Mode).IsInEnum();
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.Members).NotEmpty().Must(x => x.Any(item => item.IsEnabled))
            .WithMessage("至少需要一个启用状态的 Agent 成员。");
        RuleForEach(x => x.Members).SetValidator(new MultiAgentMemberInputValidator());
    }
}

public sealed class MultiAgentRunRequestValidator : AbstractValidator<MultiAgentRunRequest>
{
    public MultiAgentRunRequestValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(8000);
    }
}
