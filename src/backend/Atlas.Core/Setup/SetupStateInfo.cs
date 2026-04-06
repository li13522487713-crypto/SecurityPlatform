using System.Text.Json.Serialization;

namespace Atlas.Core.Setup;

/// <summary>
/// 安装状态持久化模型（序列化到 setup-state.json，不依赖数据库）。
/// </summary>
public sealed class SetupStateInfo
{
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SetupState Status { get; set; } = SetupState.NotConfigured;

    [JsonPropertyName("completedAt")]
    public DateTimeOffset? CompletedAt { get; set; }

    [JsonPropertyName("failedAt")]
    public DateTimeOffset? FailedAt { get; set; }

    [JsonPropertyName("failureMessage")]
    public string? FailureMessage { get; set; }

    /// <summary>
    /// 历史遗留字段，仅用于诊断。运行时数据库配置已迁移至 appsettings.runtime.json。
    /// </summary>
    [Obsolete("数据库配置已迁移至 appsettings.runtime.json，此字段仅保留用于诊断。")]
    [JsonPropertyName("database")]
    public SetupDatabaseInfo? Database { get; set; }

    [JsonPropertyName("platformSetupCompleted")]
    public bool PlatformSetupCompleted { get; set; }
}

/// <summary>
/// setup 阶段记录的数据库连接信息。
/// </summary>
public sealed class SetupDatabaseInfo
{
    [JsonPropertyName("dbType")]
    public string DbType { get; set; } = "SQLite";

    [JsonPropertyName("connectionString")]
    public string ConnectionString { get; set; } = string.Empty;
}
