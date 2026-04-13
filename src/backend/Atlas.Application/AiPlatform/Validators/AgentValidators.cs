using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class AgentCreateRequestValidator : AbstractValidator<AgentCreateRequest>
{
    public AgentCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024).When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.AvatarUrl).MaximumLength(1024).When(x => !string.IsNullOrWhiteSpace(x.AvatarUrl));
        RuleFor(x => x.SystemPrompt).MaximumLength(32000).When(x => !string.IsNullOrWhiteSpace(x.SystemPrompt));
        RuleFor(x => x.PersonaMarkdown).MaximumLength(12000).When(x => !string.IsNullOrWhiteSpace(x.PersonaMarkdown));
        RuleFor(x => x.Goals).MaximumLength(4000).When(x => !string.IsNullOrWhiteSpace(x.Goals));
        RuleFor(x => x.ReplyLogic).MaximumLength(8000).When(x => !string.IsNullOrWhiteSpace(x.ReplyLogic));
        RuleFor(x => x.OutputFormat).MaximumLength(4000).When(x => !string.IsNullOrWhiteSpace(x.OutputFormat));
        RuleFor(x => x.Constraints).MaximumLength(4000).When(x => !string.IsNullOrWhiteSpace(x.Constraints));
        RuleFor(x => x.OpeningMessage).MaximumLength(2000).When(x => !string.IsNullOrWhiteSpace(x.OpeningMessage));
        RuleFor(x => x.PresetQuestions).Must(questions => questions is null || questions.Count <= 6)
            .WithMessage("PresetQuestions 最多 6 条。");
        RuleForEach(x => x.PresetQuestions).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DatabaseBindingIds).Must(ids => ids is null || ids.Count <= 20)
            .WithMessage("DatabaseBindingIds 最多 20 项。");
        RuleFor(x => x.VariableBindingIds).Must(ids => ids is null || ids.Count <= 50)
            .WithMessage("VariableBindingIds 最多 50 项。");
        RuleFor(x => x.ModelName).MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.ModelName));
        RuleFor(x => x.Temperature).InclusiveBetween(0f, 2f).When(x => x.Temperature.HasValue);
        RuleFor(x => x.MaxTokens).InclusiveBetween(1, 128000).When(x => x.MaxTokens.HasValue);
        RuleFor(x => x.DefaultWorkflowId).GreaterThan(0).When(x => x.DefaultWorkflowId.HasValue);
        RuleFor(x => x.DefaultWorkflowName).MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.DefaultWorkflowName));
        RuleFor(x => x.LongTermMemoryTopK).InclusiveBetween(1, 10).When(x => x.LongTermMemoryTopK.HasValue);
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
        RuleFor(x => x.PersonaMarkdown).MaximumLength(12000).When(x => !string.IsNullOrWhiteSpace(x.PersonaMarkdown));
        RuleFor(x => x.Goals).MaximumLength(4000).When(x => !string.IsNullOrWhiteSpace(x.Goals));
        RuleFor(x => x.ReplyLogic).MaximumLength(8000).When(x => !string.IsNullOrWhiteSpace(x.ReplyLogic));
        RuleFor(x => x.OutputFormat).MaximumLength(4000).When(x => !string.IsNullOrWhiteSpace(x.OutputFormat));
        RuleFor(x => x.Constraints).MaximumLength(4000).When(x => !string.IsNullOrWhiteSpace(x.Constraints));
        RuleFor(x => x.OpeningMessage).MaximumLength(2000).When(x => !string.IsNullOrWhiteSpace(x.OpeningMessage));
        RuleFor(x => x.PresetQuestions).Must(questions => questions is null || questions.Count <= 6)
            .WithMessage("PresetQuestions 最多 6 条。");
        RuleForEach(x => x.PresetQuestions).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DatabaseBindingIds).Must(ids => ids is null || ids.Count <= 20)
            .WithMessage("DatabaseBindingIds 最多 20 项。");
        RuleFor(x => x.VariableBindingIds).Must(ids => ids is null || ids.Count <= 50)
            .WithMessage("VariableBindingIds 最多 50 项。");
        RuleFor(x => x.ModelName).MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.ModelName));
        RuleFor(x => x.Temperature).InclusiveBetween(0f, 2f).When(x => x.Temperature.HasValue);
        RuleFor(x => x.MaxTokens).InclusiveBetween(1, 128000).When(x => x.MaxTokens.HasValue);
        RuleFor(x => x.DefaultWorkflowId).GreaterThan(0).When(x => x.DefaultWorkflowId.HasValue);
        RuleFor(x => x.DefaultWorkflowName).MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.DefaultWorkflowName));
        RuleFor(x => x.LongTermMemoryTopK).InclusiveBetween(1, 10).When(x => x.LongTermMemoryTopK.HasValue);
        RuleForEach(x => x.PluginBindings).SetValidator(new AgentPluginBindingInputValidator());
    }
}

public sealed class WorkflowBindingUpdateRequestValidator : AbstractValidator<WorkflowBindingUpdateRequest>
{
    public WorkflowBindingUpdateRequestValidator()
    {
        RuleFor(x => x.WorkflowId).GreaterThan(0).When(x => x.WorkflowId.HasValue);
    }
}
