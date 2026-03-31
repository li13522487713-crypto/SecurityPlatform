using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.Validators;

public sealed class TeamAgentCreateRequestValidator : AbstractValidator<TeamAgentCreateRequest>
{
    public TeamAgentCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Members).NotNull();
        RuleForEach(x => x.Members).SetValidator(new TeamAgentMemberInputValidator());
    }
}

public sealed class TeamAgentUpdateRequestValidator : AbstractValidator<TeamAgentUpdateRequest>
{
    public TeamAgentUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Members).NotNull();
        RuleForEach(x => x.Members).SetValidator(new TeamAgentMemberInputValidator());
    }
}

public sealed class TeamAgentMemberInputValidator : AbstractValidator<TeamAgentMemberInput>
{
    public TeamAgentMemberInputValidator()
    {
        RuleFor(x => x.AgentId).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RoleName).NotEmpty().MaximumLength(100);
    }
}

public sealed class TeamAgentConversationCreateRequestValidator : AbstractValidator<TeamAgentConversationCreateRequest>
{
    public TeamAgentConversationCreateRequestValidator()
    {
        RuleFor(x => x.Title).MaximumLength(100);
    }
}

public sealed class TeamAgentConversationUpdateRequestValidator : AbstractValidator<TeamAgentConversationUpdateRequest>
{
    public TeamAgentConversationUpdateRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(100);
    }
}

public sealed class TeamAgentChatRequestValidator : AbstractValidator<TeamAgentChatRequest>
{
    public TeamAgentChatRequestValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(4000);
    }
}

public sealed class TeamAgentChatCancelRequestValidator : AbstractValidator<TeamAgentChatCancelRequest>
{
    public TeamAgentChatCancelRequestValidator()
    {
        RuleFor(x => x.ConversationId).GreaterThan(0);
    }
}

public sealed class SchemaDraftCreateRequestValidator : AbstractValidator<SchemaDraftCreateRequest>
{
    public SchemaDraftCreateRequestValidator()
    {
        RuleFor(x => x.Requirement).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Title).MaximumLength(100);
    }
}

public sealed class SchemaDraftUpdateRequestValidator : AbstractValidator<SchemaDraftUpdateRequest>
{
    public SchemaDraftUpdateRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Requirement).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.SchemaDraft).NotNull();
    }
}

public sealed class SchemaDraftConfirmationRequestValidator : AbstractValidator<SchemaDraftConfirmationRequest>
{
    public SchemaDraftConfirmationRequestValidator()
    {
        RuleFor(x => x.Confirmed).Equal(true);
    }
}
