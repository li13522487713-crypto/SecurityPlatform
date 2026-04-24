using System.Text.Json;
using Atlas.Application.AiPlatform.Models;
using Atlas.Presentation.Shared.Controllers.Ai;

namespace Atlas.SecurityPlatform.Tests.Services;

public sealed class CozeCompatGatewaySupportTests
{
    [Fact]
    public void BuildTypeListPayload_ShouldExposeJavaScriptSafeModelType()
    {
        var model = new ModelConfigDto(
            Id: 1_497_224_151_042_101_248,
            Name: "DeepSeek",
            ProviderType: "deepseek",
            BaseUrl: "https://api.deepseek.com",
            DefaultModel: "deepseek-v4-flash",
            ModelId: "deepseek-v4-flash",
            SystemPrompt: null,
            IsEnabled: true,
            SupportsEmbedding: false,
            EnableStreaming: true,
            EnableReasoning: false,
            EnableTools: false,
            EnableVision: false,
            EnableJsonMode: false,
            Temperature: 0,
            MaxTokens: 2048,
            TopP: null,
            FrequencyPenalty: null,
            PresencePenalty: null,
            ApiKeyMasked: "***",
            CreatedAt: DateTime.UtcNow);

        var payload = CozeCompatGatewaySupport.BuildTypeListPayload([model], modelScene: null);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        var firstModel = document.RootElement.GetProperty("model_list")[0];
        var modelType = firstModel.GetProperty("model_type").GetInt32();

        Assert.InRange(modelType, 1000, 2_000_000_999);
        Assert.NotEqual(model.Id, modelType);
        Assert.Equal(model.Id.ToString(), firstModel.GetProperty("model_config_id").GetString());
        Assert.Equal(modelType, document.RootElement.GetProperty("default_model_id").GetInt32());
    }
}
