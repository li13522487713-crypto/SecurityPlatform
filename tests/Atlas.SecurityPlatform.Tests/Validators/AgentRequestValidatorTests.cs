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
            "StructuredBot",
            "安全助手",
            "https://example.com/avatar.png",
            "system prompt",
            "persona markdown",
            "goals",
            "reply logic",
            "output format",
            "constraints",
            "opening message",
            ["问题一", "问题二"],
            null,
            null,
            0.5f,
            2048,
            null,
            null,
            true,
            true,
            true,
            3);

        var result = _createValidator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateValidator_ShouldFail_WhenPresetQuestionsExceedLimit()
    {
        var request = new AgentUpdateRequest(
            "StructuredBot",
            "安全助手",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            ["1", "2", "3", "4", "5", "6", "7"],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        var result = _updateValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AgentUpdateRequest.PresetQuestions));
    }
}
