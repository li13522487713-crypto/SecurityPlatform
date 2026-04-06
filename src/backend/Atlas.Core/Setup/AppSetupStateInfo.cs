using System.Text.Json.Serialization;

namespace Atlas.Core.Setup;

/// <summary>
/// 应用级安装状态持久化模型（序列化到 app-setup-state.json）。
/// </summary>
public sealed class AppSetupStateInfo
{
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AppSetupState Status { get; set; } = AppSetupState.NotConfigured;

    [JsonPropertyName("completedAt")]
    public DateTimeOffset? CompletedAt { get; set; }

    [JsonPropertyName("failedAt")]
    public DateTimeOffset? FailedAt { get; set; }

    [JsonPropertyName("failureMessage")]
    public string? FailureMessage { get; set; }

    [JsonPropertyName("appName")]
    public string? AppName { get; set; }

    [JsonPropertyName("adminUsername")]
    public string? AdminUsername { get; set; }
}
