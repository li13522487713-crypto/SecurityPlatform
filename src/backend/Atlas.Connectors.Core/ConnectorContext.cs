namespace Atlas.Connectors.Core;

/// <summary>
/// 调用任意 Provider 接口时必须传入的上下文：标识当前是哪个租户、哪个 provider 实例。
/// 避免 provider 内部反向依赖 ITenantProvider，从而保持 Connectors.* 库的可独立测试性。
/// </summary>
public sealed record ConnectorContext
{
    public required Guid TenantId { get; init; }

    /// <summary>
    /// 在 ConnectorRegistry 中已注册的 provider 实例 ID（对应 ExternalIdentityProvider.Id）。
    /// </summary>
    public required long ProviderInstanceId { get; init; }

    public required string ProviderType { get; init; }

    /// <summary>
    /// 已解密的运行时配置载荷（如 WeComRuntimeOptions / FeishuRuntimeOptions / DingTalkRuntimeOptions）。
    /// 由调用方（Application/Infrastructure 层，Scoped）通过 IConnectorRuntimeOptionsAccessor 解析后注入；
    /// Connectors.* 底层库（Singleton）不再反向依赖任何 Scoped 仓储，彻底消除 captive dependency。
    /// 各 Provider 内部按 ProviderType 强制 cast 为自身 RuntimeOptions 类型；类型不匹配抛 ProviderConfigInvalid。
    /// </summary>
    public required object RuntimeOptions { get; init; }

    /// <summary>
    /// 用于 trace / 日志 / 审计的关联 ID；缺省由调用方生成。
    /// </summary>
    public string TraceId { get; init; } = Guid.NewGuid().ToString("N");
}
