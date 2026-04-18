using System.Collections.Concurrent;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Options;

namespace Atlas.Connectors.Core;

/// <summary>
/// 进程级 ConnectorRegistry：按 ProviderType 路由到具体的 4 大能力实现。
/// 同一进程内每个 ProviderType 只允许一个实现注册，重复注册抛异常以避免静默覆盖。
/// </summary>
public interface IConnectorRegistry
{
    void Register(ProviderDescriptor descriptor);

    IReadOnlyList<ProviderDescriptor> ListDescriptors();

    ProviderDescriptor GetDescriptor(string providerType);

    bool TryGetDescriptor(string providerType, out ProviderDescriptor? descriptor);

    IExternalIdentityProvider GetIdentity(string providerType);

    IExternalDirectoryProvider GetDirectory(string providerType);

    IExternalApprovalProvider GetApproval(string providerType);

    IExternalMessagingProvider GetMessaging(string providerType);

    IConnectorEventVerifier GetEventVerifier(string providerType);
}

/// <summary>
/// 默认实现：基于已注入的 IEnumerable&lt;IExternalXxxProvider&gt; 按 ProviderType 建立路由表。
/// </summary>
public sealed class ConnectorRegistry : IConnectorRegistry
{
    private readonly ConcurrentDictionary<string, ProviderDescriptor> _descriptors = new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyDictionary<string, IExternalIdentityProvider> _identityProviders;
    private readonly IReadOnlyDictionary<string, IExternalDirectoryProvider> _directoryProviders;
    private readonly IReadOnlyDictionary<string, IExternalApprovalProvider> _approvalProviders;
    private readonly IReadOnlyDictionary<string, IExternalMessagingProvider> _messagingProviders;
    private readonly IReadOnlyDictionary<string, IConnectorEventVerifier> _eventVerifiers;

    public ConnectorRegistry(
        IEnumerable<IExternalIdentityProvider> identityProviders,
        IEnumerable<IExternalDirectoryProvider> directoryProviders,
        IEnumerable<IExternalApprovalProvider> approvalProviders,
        IEnumerable<IExternalMessagingProvider> messagingProviders,
        IEnumerable<IConnectorEventVerifier> eventVerifiers,
        IEnumerable<ProviderDescriptor>? initialDescriptors = null)
    {
        _identityProviders = ToDict(identityProviders, p => p.ProviderType);
        _directoryProviders = ToDict(directoryProviders, p => p.ProviderType);
        _approvalProviders = ToDict(approvalProviders, p => p.ProviderType);
        _messagingProviders = ToDict(messagingProviders, p => p.ProviderType);
        _eventVerifiers = ToDict(eventVerifiers, p => p.ProviderType);

        if (initialDescriptors is not null)
        {
            foreach (var d in initialDescriptors)
            {
                Register(d);
            }
        }
    }

    public void Register(ProviderDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!_descriptors.TryAdd(descriptor.ProviderType, descriptor))
        {
            throw new InvalidOperationException($"Provider descriptor '{descriptor.ProviderType}' already registered.");
        }
    }

    public IReadOnlyList<ProviderDescriptor> ListDescriptors()
        => _descriptors.Values.OrderBy(d => d.ProviderType, StringComparer.OrdinalIgnoreCase).ToList();

    public ProviderDescriptor GetDescriptor(string providerType)
    {
        if (_descriptors.TryGetValue(providerType, out var d))
        {
            return d;
        }
        throw new ConnectorException(ConnectorErrorCodes.ProviderNotFound, $"Connector provider '{providerType}' is not registered.");
    }

    public bool TryGetDescriptor(string providerType, out ProviderDescriptor? descriptor)
        => _descriptors.TryGetValue(providerType, out descriptor);

    public IExternalIdentityProvider GetIdentity(string providerType) => Resolve(_identityProviders, providerType);

    public IExternalDirectoryProvider GetDirectory(string providerType) => Resolve(_directoryProviders, providerType);

    public IExternalApprovalProvider GetApproval(string providerType) => Resolve(_approvalProviders, providerType);

    public IExternalMessagingProvider GetMessaging(string providerType) => Resolve(_messagingProviders, providerType);

    public IConnectorEventVerifier GetEventVerifier(string providerType) => Resolve(_eventVerifiers, providerType);

    private static T Resolve<T>(IReadOnlyDictionary<string, T> dict, string providerType)
    {
        if (dict.TryGetValue(providerType, out var instance))
        {
            return instance;
        }
        throw new ConnectorException(ConnectorErrorCodes.ProviderNotFound, $"Connector provider '{providerType}' has no registered {typeof(T).Name}.");
    }

    private static IReadOnlyDictionary<string, T> ToDict<T>(IEnumerable<T> instances, Func<T, string> keySelector)
    {
        var dict = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var instance in instances)
        {
            var key = keySelector(instance);
            if (!dict.TryAdd(key, instance))
            {
                throw new InvalidOperationException($"Duplicate connector provider '{key}' for {typeof(T).Name}.");
            }
        }
        return dict;
    }
}
