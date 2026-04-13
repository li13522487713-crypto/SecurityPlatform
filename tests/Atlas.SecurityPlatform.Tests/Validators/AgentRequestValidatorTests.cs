using Atlas.Application.AiPlatform.Models;
using Atlas.Application.AiPlatform.Validators;

namespace Atlas.SecurityPlatform.Tests.Validators;

public sealed class AgentRequestValidatorTests
{
    private readonly AgentCreateRequestValidator _createValidator = new();
    private readonly AgentUpdateRequestValidator _updateValidator = new();

    [Fact]
    public void CreateValidator_ShouldPass_WhenStructuredBotFieldsAreValid()
    {
        var request = new AgentCreateRequest(
            Name: "StructuredBot",
            Description: "安全助手",
            AvatarUrl: "https://example.com/avatar.png",
            SystemPrompt: "system prompt",
            PersonaMarkdown: "persona markdown",
            Goals: "goals",
            ReplyLogic: "reply logic",
            OutputFormat: "output format",
            Constraints: "constraints",
            OpeningMessage: "opening message",
            PresetQuestions: ["问题一", "问题二"],
            DatabaseBindingIds: null,
            VariableBindingIds: null,
            ModelConfigId: null,
            ModelName: null,
            Temperature: 0.5f,
            MaxTokens: 2048,
            DefaultWorkflowId: null,
            DefaultWorkflowName: null,
            EnableMemory: true,
            EnableShortTermMemory: true,
            EnableLongTermMemory: true,
            LongTermMemoryTopK: 3);

        var result = _createValidator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateValidator_ShouldFail_WhenPresetQuestionsExceedLimit()
    {
        var request = new AgentUpdateRequest(
            Name: "StructuredBot",
            Description: "安全助手",
            AvatarUrl: null,
            SystemPrompt: null,
            PersonaMarkdown: null,
            Goals: null,
            ReplyLogic: null,
            OutputFormat: null,
            Constraints: null,
            OpeningMessage: null,
            PresetQuestions: ["1", "2", "3", "4", "5", "6", "7"],
            DatabaseBindingIds: null,
            VariableBindingIds: null,
            ModelConfigId: null,
            ModelName: null,
            Temperature: null,
            MaxTokens: null,
            DefaultWorkflowId: null,
            DefaultWorkflowName: null,
            EnableMemory: null,
            EnableShortTermMemory: null,
            EnableLongTermMemory: null,
            LongTermMemoryTopK: null,
            KnowledgeBaseIds: null,
            PluginBindings: null);

        var result = _updateValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AgentUpdateRequest.PresetQuestions));
    }
}
