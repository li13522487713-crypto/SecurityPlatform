namespace Atlas.Connectors.Core.Abstractions;

/// <summary>
/// 把 ConnectorContext（TenantId + ProviderInstanceId）解析为 provider 私有的运行时配置（包含已解密凭据）。
/// 由 Infrastructure.ExternalConnectors 提供实现，Connectors.* 库不直接依赖加解密细节，保持可独立测试。
/// </summary>
public interface IConnectorRuntimeOptionsResolver<TRuntimeOptions> where TRuntimeOptions : class
{
    Task<TRuntimeOptions> ResolveAsync(ConnectorContext context, CancellationToken cancellationToken);
}
