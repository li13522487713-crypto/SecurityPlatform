using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Caching;
using Atlas.Connectors.Core.Security;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.ExternalConnectors;

public sealed class ConnectorsCoreTests
{
    [Fact]
    public async Task TokenCache_GetOrCreate_DeduplicatesConcurrentFactoryCalls()
    {
        var cache = new InMemoryConnectorTokenCache(new MemoryCache(new MemoryCacheOptions()));
        var counter = 0;

        async Task<string> Resolve()
        {
            return await cache.GetOrCreateAsync<string>(
                "atlas-test:token",
                async _ =>
                {
                    Interlocked.Increment(ref counter);
                    await Task.Delay(20);
                    return ("token-value", TimeSpan.FromMinutes(5));
                },
                CancellationToken.None);
        }

        var results = await Task.WhenAll(Resolve(), Resolve(), Resolve(), Resolve());

        Assert.All(results, r => Assert.Equal("token-value", r));
        Assert.Equal(1, counter);
    }

    [Fact]
    public async Task OAuthState_ConsumeAsync_IsSingleUse()
    {
        var store = new InMemoryOAuthStateStore();
        var state = new OAuthState
        {
            Value = OAuthState.CreateValue(),
            TenantId = Guid.NewGuid(),
            ProviderInstanceId = 1,
            ProviderType = "wecom",
            RedirectUri = "https://example.com/cb",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
        };
        await store.SaveAsync(state, CancellationToken.None);

        var first = await store.ConsumeAsync(state.Value, CancellationToken.None);
        var second = await store.ConsumeAsync(state.Value, CancellationToken.None);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public void HmacValidator_WeComSignature_StableAndOrderInsensitive()
    {
        const string token = "QDG6eK";
        const string timestamp = "1409659813";
        const string nonce = "1372623149";
        const string body = "EncryptedTextSample";

        var sig1 = HmacValidator.ComputeWeComStyleSignature(token, timestamp, nonce, body);
        var sig2 = HmacValidator.ComputeWeComStyleSignature(token, nonce, timestamp, body);
        Assert.Equal(sig1, sig2);
        Assert.Equal(40, sig1.Length); // SHA1 hex
    }

    [Fact]
    public void ReplayGuard_RejectsDuplicateAndOldEvents()
    {
        var guard = new InMemoryReplayGuard();
        var now = DateTimeOffset.UtcNow;
        Assert.True(guard.TryAccept("event-1", now, TimeSpan.FromMinutes(5)));
        Assert.False(guard.TryAccept("event-1", now, TimeSpan.FromMinutes(5)));
        Assert.False(guard.TryAccept("event-2", now.AddMinutes(-10), TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void ConnectorRegistry_ThrowsConnectorException_WhenProviderMissing()
    {
        var registry = new ConnectorRegistry(
            identityProviders: Array.Empty<Atlas.Connectors.Core.Abstractions.IExternalIdentityProvider>(),
            directoryProviders: Array.Empty<Atlas.Connectors.Core.Abstractions.IExternalDirectoryProvider>(),
            approvalProviders: Array.Empty<Atlas.Connectors.Core.Abstractions.IExternalApprovalProvider>(),
            messagingProviders: Array.Empty<Atlas.Connectors.Core.Abstractions.IExternalMessagingProvider>(),
            eventVerifiers: Array.Empty<Atlas.Connectors.Core.Abstractions.IConnectorEventVerifier>());

        var ex = Assert.Throws<ConnectorException>(() => registry.GetIdentity("wecom"));
        Assert.Equal(ConnectorErrorCodes.ProviderNotFound, ex.Code);
    }
}
