namespace Atlas.Application.ExternalConnectors.Abstractions;

/// <summary>
/// 类型化的 RuntimeOptions 解析端口，由各 provider 在 Infrastructure 层实现：
/// 读取 ExternalIdentityProvider 实体 + 解密 SecretJson → 构造对应的 typed RuntimeOptions
/// （如 WeComRuntimeOptions / FeishuRuntimeOptions / DingTalkRuntimeOptions）。
///
/// 该端口位于 Application 层，明确归属调用方（Scoped），底层 Connectors.* 库不再反向依赖此端口，
/// 从而消除「Singleton ApiClient 持有 Scoped Resolver」的 captive dependency。
/// </summary>
/// <typeparam name="TRuntimeOptions">具体 provider 的 RuntimeOptions 类型，如 WeComRuntimeOptions。</typeparam>
public interface IConnectorRuntimeOptionsResolver<TRuntimeOptions> where TRuntimeOptions : class
{
    Task<TRuntimeOptions> ResolveAsync(Guid tenantId, long providerInstanceId, CancellationToken cancellationToken);
}
