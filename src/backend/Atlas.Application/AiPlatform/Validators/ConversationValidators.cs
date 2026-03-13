using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class ConversationCreateRequestValidator : AbstractValidator<ConversationCreateRequest>
{
    public ConversationCreateRequestValidator()
    {
        RuleFor(x => x.AgentId).GreaterThan(0);
        RuleFor(x => x.Title).MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Title));
    }
}

public sealed class ConversationUpdateRequestValidator : AbstractValidator<ConversationUpdateRequest>
{
    public ConversationUpdateRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
    }
}

public sealed class AgentChatRequestValidator : AbstractValidator<AgentChatRequest>
{
    public AgentChatRequestValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(32000);
        RuleFor(x => x.ConversationId).GreaterThan(0).When(x => x.ConversationId.HasValue);
    }
}

public sealed class AgentChatCancelRequestValidator : AbstractValidator<AgentChatCancelRequest>
{
    public AgentChatCancelRequestValidator()
    {
        RuleFor(x => x.ConversationId).GreaterThan(0);
    }
}
