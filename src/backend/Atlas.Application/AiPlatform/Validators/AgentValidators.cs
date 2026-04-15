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
        RuleFor(x => x.DatabaseBindings).Must(items => items is null || items.Count <= 20)
            .WithMessage("DatabaseBindings 最多 20 项。");
        RuleFor(x => x.VariableBindings).Must(items => items is null || items.Count <= 50)
            .WithMessage("VariableBindings 最多 50 项。");
        RuleFor(x => x.DatabaseBindingIds).Must(ids => ids is null || ids.Count <= 20)
            .WithMessage("DatabaseBindingIds 最多 20 项。");
        RuleFor(x => x.VariableBindingIds).Must(ids => ids is null || ids.Count <= 50)
            .WithMessage("VariableBindingIds 最多 50 项。");
        RuleForEach(x => x.KnowledgeBindings).SetValidator(new AgentKnowledgeBindingInputValidator());
        RuleForEach(x => x.DatabaseBindings).SetValidator(new AgentDatabaseBindingInputValidator());
        RuleForEach(x => x.VariableBindings).SetValidator(new AgentVariableBindingInputValidator());
        RuleFor(x => x.ModelName).MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.ModelName));
        RuleFor(x => x.Temperature).InclusiveBetween(0f, 2f).When(x => x.Temperature.HasValue);
        RuleFor(x => x.MaxTokens).InclusiveBetween(1, 128000).When(x => x.MaxTokens.HasValue);
        RuleFor(x => x.WorkspaceId).GreaterThan(0).When(x => x.WorkspaceId.HasValue);
        RuleFor(x => x.DefaultWorkflowId).GreaterThan(0).When(x => x.DefaultWorkflowId.HasValue);
        RuleFor(x => x.DefaultWorkflowName).MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.DefaultWorkflowName));
        RuleFor(x => x.LongTermMemoryTopK).InclusiveBetween(1, 10).When(x => x.LongTermMemoryTopK.HasValue);
    }
}

public sealed class AgentPluginParameterBindingInputValidator : AbstractValidator<AgentPluginParameterBindingInput>
{
    public AgentPluginParameterBindingInputValidator()
    {
        RuleFor(x => x.ParameterName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ValueSource).Must(value => value is "literal" or "variable");
        RuleFor(x => x.LiteralValue).NotEmpty().When(x => x.ValueSource == "literal");
        RuleFor(x => x.VariableKey).NotEmpty().MaximumLength(128).When(x => x.ValueSource == "variable");
    }
}

public sealed class AgentPluginToolBindingInputValidator : AbstractValidator<AgentPluginToolBindingInput>
{
    public AgentPluginToolBindingInputValidator()
    {
        RuleFor(x => x.ApiId).GreaterThan(0);
        RuleFor(x => x.TimeoutSeconds).InclusiveBetween(1, 300);
        RuleFor(x => x.FailurePolicy).Must(value => value is "skip" or "fail");
        RuleForEach(x => x.ParameterBindings).SetValidator(new AgentPluginParameterBindingInputValidator());
    }
}

public sealed class AgentPluginBindingInputValidator : AbstractValidator<AgentPluginBindingInput>
{
    public AgentPluginBindingInputValidator()
    {
        RuleFor(x => x.PluginId).GreaterThan(0);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ToolConfigJson).MaximumLength(500000).When(x => x.ToolConfigJson is not null);
        RuleForEach(x => x.ToolBindings).SetValidator(new AgentPluginToolBindingInputValidator());
    }
}

public sealed class AgentKnowledgeBindingInputValidator : AbstractValidator<AgentKnowledgeBindingInput>
{
    public AgentKnowledgeBindingInputValidator()
    {
        RuleFor(x => x.KnowledgeBaseId).GreaterThan(0);
        RuleFor(x => x.InvokeMode).Must(value => value is "auto" or "manual");
        RuleFor(x => x.TopK).InclusiveBetween(1, 20);
        RuleFor(x => x.ScoreThreshold).InclusiveBetween(0d, 1d).When(x => x.ScoreThreshold.HasValue);
        RuleFor(x => x.EnabledContentTypes).Must(values =>
            values is null || values.All(value => value is "text" or "table" or "image"));
    }
}

public sealed class AgentDatabaseBindingInputValidator : AbstractValidator<AgentDatabaseBindingInput>
{
    public AgentDatabaseBindingInputValidator()
    {
        RuleFor(x => x.DatabaseId).GreaterThan(0);
        RuleFor(x => x.Alias).MaximumLength(128).When(x => !string.IsNullOrWhiteSpace(x.Alias));
        RuleFor(x => x.AccessMode).Must(value => value is "readonly" or "readwrite");
        RuleFor(x => x.TableAllowlist).Must(values => values is null || values.Count <= 50)
            .WithMessage("TableAllowlist 最多 50 项。");
        RuleForEach(x => x.TableAllowlist).NotEmpty().MaximumLength(128);
    }
}

public sealed class AgentVariableBindingInputValidator : AbstractValidator<AgentVariableBindingInput>
{
    public AgentVariableBindingInputValidator()
    {
        RuleFor(x => x.VariableId).GreaterThan(0);
        RuleFor(x => x.Alias).MaximumLength(128).When(x => !string.IsNullOrWhiteSpace(x.Alias));
        RuleFor(x => x.DefaultValueOverride).MaximumLength(4000).When(x => !string.IsNullOrWhiteSpace(x.DefaultValueOverride));
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
        RuleFor(x => x.DatabaseBindings).Must(items => items is null || items.Count <= 20)
            .WithMessage("DatabaseBindings 最多 20 项。");
        RuleFor(x => x.VariableBindings).Must(items => items is null || items.Count <= 50)
            .WithMessage("VariableBindings 最多 50 项。");
        RuleFor(x => x.DatabaseBindingIds).Must(ids => ids is null || ids.Count <= 20)
            .WithMessage("DatabaseBindingIds 最多 20 项。");
        RuleFor(x => x.VariableBindingIds).Must(ids => ids is null || ids.Count <= 50)
            .WithMessage("VariableBindingIds 最多 50 项。");
        RuleForEach(x => x.KnowledgeBindings).SetValidator(new AgentKnowledgeBindingInputValidator());
        RuleForEach(x => x.DatabaseBindings).SetValidator(new AgentDatabaseBindingInputValidator());
        RuleForEach(x => x.VariableBindings).SetValidator(new AgentVariableBindingInputValidator());
        RuleFor(x => x.ModelName).MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.ModelName));
        RuleFor(x => x.Temperature).InclusiveBetween(0f, 2f).When(x => x.Temperature.HasValue);
        RuleFor(x => x.MaxTokens).InclusiveBetween(1, 128000).When(x => x.MaxTokens.HasValue);
        RuleFor(x => x.WorkspaceId).GreaterThan(0).When(x => x.WorkspaceId.HasValue);
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
