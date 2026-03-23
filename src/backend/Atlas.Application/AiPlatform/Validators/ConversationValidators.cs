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
        RuleFor(x => x.Message).MaximumLength(32000).When(x => !string.IsNullOrWhiteSpace(x.Message));
        RuleFor(x => x.ConversationId).GreaterThan(0).When(x => x.ConversationId.HasValue);
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Message) || (x.Attachments?.Count ?? 0) > 0)
            .WithMessage("消息内容或附件至少提供一项。");
        RuleForEach(x => x.Attachments!).SetValidator(new AgentChatAttachmentValidator())
            .When(x => x.Attachments is not null);
    }
}

public sealed class AgentChatAttachmentValidator : AbstractValidator<AgentChatAttachment>
{
    public AgentChatAttachmentValidator()
    {
        RuleFor(x => x.Type).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Url).MaximumLength(1024).When(x => !string.IsNullOrWhiteSpace(x.Url));
        RuleFor(x => x.FileId).MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.FileId));
        RuleFor(x => x.MimeType).MaximumLength(128).When(x => !string.IsNullOrWhiteSpace(x.MimeType));
        RuleFor(x => x.Name).MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Name));
        RuleFor(x => x.Text).MaximumLength(2000).When(x => !string.IsNullOrWhiteSpace(x.Text));
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Url) || !string.IsNullOrWhiteSpace(x.FileId) || !string.IsNullOrWhiteSpace(x.Text))
            .WithMessage("附件至少提供 url/fileId/text 之一。");
    }
}

public sealed class AgentChatCancelRequestValidator : AbstractValidator<AgentChatCancelRequest>
{
    public AgentChatCancelRequestValidator()
    {
        RuleFor(x => x.ConversationId).GreaterThan(0);
    }
}
