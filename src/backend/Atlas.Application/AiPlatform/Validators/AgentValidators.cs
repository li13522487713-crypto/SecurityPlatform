using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class AgentCreateRequestValidator : AbstractValidator<AgentCreateRequest>
{
    public AgentCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024).When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.SystemPrompt).MaximumLength(32000).When(x => !string.IsNullOrWhiteSpace(x.SystemPrompt));
        RuleFor(x => x.ModelName).MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.ModelName));
        RuleFor(x => x.Temperature).InclusiveBetween(0f, 2f).When(x => x.Temperature.HasValue);
        RuleFor(x => x.MaxTokens).InclusiveBetween(1, 128000).When(x => x.MaxTokens.HasValue);
    }
}

public sealed class AgentPluginBindingInputValidator : AbstractValidator<AgentPluginBindingInput>
{
    public AgentPluginBindingInputValidator()
    {
        RuleFor(x => x.PluginId).GreaterThan(0);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ToolConfigJson).MaximumLength(500000).When(x => x.ToolConfigJson is not null);
    }
}

public sealed class AgentUpdateRequestValidator : AbstractValidator<AgentUpdateRequest>
{
    public AgentUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024).When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.AvatarUrl).MaximumLength(1024).When(x => !string.IsNullOrWhiteSpace(x.AvatarUrl));
        RuleFor(x => x.SystemPrompt).MaximumLength(32000).When(x => !string.IsNullOrWhiteSpace(x.SystemPrompt));
        RuleFor(x => x.ModelName).MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.ModelName));
        RuleFor(x => x.Temperature).InclusiveBetween(0f, 2f).When(x => x.Temperature.HasValue);
        RuleFor(x => x.MaxTokens).InclusiveBetween(1, 128000).When(x => x.MaxTokens.HasValue);
        RuleForEach(x => x.PluginBindings).SetValidator(new AgentPluginBindingInputValidator());
    }
}
