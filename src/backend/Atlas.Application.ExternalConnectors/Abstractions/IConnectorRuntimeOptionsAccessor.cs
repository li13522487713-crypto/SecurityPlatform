namespace Atlas.Application.ExternalConnectors.Abstractions;

/// <summary>
/// 非泛型的 RuntimeOptions 解析门面：按 ProviderType 派发到具体 typed
/// <see cref="IConnectorRuntimeOptionsResolver{TRuntimeOptions}"/>，并在同一 Scope 内对 (tenantId, providerInstanceId)
/// 做 memoize，避免审批 fanout / OAuth 回调等场景重复查 DB / 重复解密。
///
/// 调用方（Application/Infrastructure 层 Scoped Service）调用本接口拿到已解密的 object 形态 RuntimeOptions，
/// 然后通过 <c>ConnectorContext.RuntimeOptions</c> 传递给底层 Connectors.* Provider；后者按自身 ProviderType
/// 强制 cast 为对应 typed RuntimeOptions。
/// </summary>
public interface IConnectorRuntimeOptionsAccessor
{
    /// <summary>
    /// 按 ProviderType 派发并解析 RuntimeOptions。返回已解密的 typed RuntimeOptions 实例（box 为 object）。
    /// 调用方应直接把返回值赋给 <c>ConnectorContext.RuntimeOptions</c>。
    /// </summary>
    Task<object> ResolveAsync(Guid tenantId, long providerInstanceId, string providerType, CancellationToken cancellationToken);
}
