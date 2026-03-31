using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.Validators;

public sealed class TeamAgentCreateRequestValidator : AbstractValidator<TeamAgentCreateRequest>
{
    public TeamAgentCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Members)
            .NotNull()
            .Must(HaveEnabledBoundMember)
            .WithMessage("Team Agent 至少需要一个已启用且已绑定单 Agent 的成员。");
        RuleForEach(x => x.Members).SetValidator(new TeamAgentMemberInputValidator());
    }

    private static bool HaveEnabledBoundMember(IReadOnlyList<TeamAgentMemberInput>? members)
        => members is not null && members.Any(member => member.IsEnabled && member.AgentId.HasValue && member.AgentId.Value > 0);
}

public sealed class TeamAgentUpdateRequestValidator : AbstractValidator<TeamAgentUpdateRequest>
{
    public TeamAgentUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Members)
            .NotNull()
            .Must(HaveEnabledBoundMember)
            .WithMessage("Team Agent 至少需要一个已启用且已绑定单 Agent 的成员。");
        RuleForEach(x => x.Members).SetValidator(new TeamAgentMemberInputValidator());
    }

    private static bool HaveEnabledBoundMember(IReadOnlyList<TeamAgentMemberInput>? members)
        => members is not null && members.Any(member => member.IsEnabled && member.AgentId.HasValue && member.AgentId.Value > 0);
}

public sealed class TeamAgentMemberInputValidator : AbstractValidator<TeamAgentMemberInput>
{
    public TeamAgentMemberInputValidator()
    {
        RuleFor(x => x.RoleName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AgentId)
            .GreaterThan(0)
            .When(x => x.AgentId.HasValue);
        RuleFor(x => x)
            .Must(member => !member.IsEnabled || (member.AgentId.HasValue && member.AgentId.Value > 0))
            .WithMessage("启用成员必须绑定单 Agent。");
    }
}

public sealed class TeamAgentConversationCreateRequestValidator : AbstractValidator<TeamAgentConversationCreateRequest>
{
    public TeamAgentConversationCreateRequestValidator()
    {
        RuleFor(x => x.Title).MaximumLength(100);
    }
}

public sealed class TeamAgentCreateFromTemplateRequestValidator : AbstractValidator<TeamAgentCreateFromTemplateRequest>
{
    public TeamAgentCreateFromTemplateRequestValidator()
    {
        RuleFor(x => x.TemplateKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleForEach(x => x.MemberBindings!).ChildRules(child =>
            {
                child.RuleFor(x => x.RoleName).NotEmpty().MaximumLength(100);
                child.RuleFor(x => x.AgentId)
                    .GreaterThan(0)
                    .When(x => x.AgentId.HasValue);
            })
            .When(x => x.MemberBindings is not null);
    }
}

public sealed class TeamAgentPublicationPublishRequestValidator : AbstractValidator<TeamAgentPublicationPublishRequest>
{
    public TeamAgentPublicationPublishRequestValidator()
    {
        RuleFor(x => x.ReleaseNote).MaximumLength(500);
    }
}

public sealed class TeamAgentLegacyMigrationRequestValidator : AbstractValidator<TeamAgentLegacyMigrationRequest>
{
    public TeamAgentLegacyMigrationRequestValidator()
    {
        RuleForEach(x => x.LegacyIds!)
            .GreaterThan(0)
            .When(x => x.LegacyIds is not null);
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
