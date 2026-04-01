using Atlas.Application.AgentTeam.Models;
using FluentValidation;

namespace Atlas.Application.AgentTeam.Validators;

public sealed class AgentTeamCreateRequestValidator : AbstractValidator<AgentTeamCreateRequest>
{
    public AgentTeamCreateRequestValidator()
    {
        RuleFor(x => x.TeamName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024).When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.Owner).NotEmpty().MaximumLength(128);
    }
}

public sealed class AgentTeamUpdateRequestValidator : AbstractValidator<AgentTeamUpdateRequest>
{
    public AgentTeamUpdateRequestValidator()
    {
        RuleFor(x => x.TeamName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024).When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.Owner).NotEmpty().MaximumLength(128);
    }
}

public sealed class SubAgentCreateRequestValidator : AbstractValidator<SubAgentCreateRequest>
{
    public SubAgentCreateRequestValidator()
    {
        RuleFor(x => x.AgentName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Role).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Goal).NotEmpty().MaximumLength(1024);
        RuleFor(x => x.PromptTemplate).NotEmpty().MaximumLength(8000);
        RuleFor(x => x.ModelConfigJson).NotEmpty();
        RuleFor(x => x.InputSchemaJson).NotEmpty();
        RuleFor(x => x.OutputSchemaJson).NotEmpty();
        RuleFor(x => x.TimeoutPolicyJson).NotEmpty();
    }
}

public sealed class SubAgentUpdateRequestValidator : AbstractValidator<SubAgentUpdateRequest>
{
    public SubAgentUpdateRequestValidator()
    {
        RuleFor(x => x.AgentName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Role).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Goal).NotEmpty().MaximumLength(1024);
        RuleFor(x => x.PromptTemplate).NotEmpty().MaximumLength(8000);
    }
}

public sealed class OrchestrationNodeCreateRequestValidator : AbstractValidator<OrchestrationNodeCreateRequest>
{
    public OrchestrationNodeCreateRequestValidator()
    {
        RuleFor(x => x.NodeName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.InputBindingJson).NotEmpty();
        RuleFor(x => x.OutputBindingJson).NotEmpty();
        RuleFor(x => x.RetryRuleJson).NotEmpty();
        RuleFor(x => x.TimeoutRuleJson).NotEmpty();
        RuleFor(x => x.Priority).GreaterThanOrEqualTo(0);
    }
}

public sealed class OrchestrationNodeUpdateRequestValidator : AbstractValidator<OrchestrationNodeUpdateRequest>
{
    public OrchestrationNodeUpdateRequestValidator()
    {
        RuleFor(x => x.NodeName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.InputBindingJson).NotEmpty();
        RuleFor(x => x.OutputBindingJson).NotEmpty();
        RuleFor(x => x.RetryRuleJson).NotEmpty();
        RuleFor(x => x.TimeoutRuleJson).NotEmpty();
        RuleFor(x => x.Priority).GreaterThanOrEqualTo(0);
    }
}

public sealed class AgentTeamRunCreateRequestValidator : AbstractValidator<AgentTeamRunCreateRequest>
{
    public AgentTeamRunCreateRequestValidator()
    {
        RuleFor(x => x.TeamId).GreaterThan(0);
        RuleFor(x => x.TeamVersionId).GreaterThan(0);
        RuleFor(x => x.InputPayloadJson).NotEmpty();
    }
}

public sealed class AgentTeamRunInterveneRequestValidator : AbstractValidator<AgentTeamRunInterveneRequest>
{
    public AgentTeamRunInterveneRequestValidator()
    {
        RuleFor(x => x.Action)
            .NotEmpty()
            .Must(action => action is "confirm" or "skip" or "retry" or "override")
            .WithMessage("Action must be one of confirm/skip/retry/override.");
    }
}

public sealed class TeamPublishRequestValidator : AbstractValidator<TeamPublishRequest>
{
    public TeamPublishRequestValidator()
    {
        RuleFor(x => x.ReleaseNote).MaximumLength(2000).When(x => !string.IsNullOrWhiteSpace(x.ReleaseNote));
        RuleFor(x => x.ApprovalRecordId).MaximumLength(128).When(x => !string.IsNullOrWhiteSpace(x.ApprovalRecordId));
    }
}

public sealed class AgentTeamDebugRequestValidator : AbstractValidator<AgentTeamDebugRequest>
{
    public AgentTeamDebugRequestValidator()
    {
        RuleFor(x => x.InputPayloadJson).NotEmpty();
    }
}
