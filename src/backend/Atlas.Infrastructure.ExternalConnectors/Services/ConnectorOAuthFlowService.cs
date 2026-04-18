using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Application.ExternalConnectors.Models;
using Atlas.Application.ExternalConnectors.Repositories;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Security;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Enums;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.ExternalConnectors.Services;

/// <summary>
/// OAuth start / callback 的薄编排：
/// - start: 解析 provider → 生成 state → 调用 IdentityProvider.BuildAuthorizationUrl；
/// - callback: 校验 state（单次使用 + 跨租户校验）→ 调用 IdentityProvider.ExchangeCode → 命中 BindingService → 签发 JWT 或返回待绑定 ticket。
/// </summary>
public sealed class ConnectorOAuthFlowService : IConnectorOAuthFlowService
{
    private readonly IConnectorRegistry _registry;
    private readonly IExternalIdentityProviderRepository _providerRepository;
    private readonly IExternalIdentityBindingService _bindingService;
    private readonly IConnectorJwtIssuer _jwtIssuer;
    private readonly IOAuthStateStore _stateStore;
    private readonly ITenantProvider _tenantProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ConnectorOAuthFlowService> _logger;

    public ConnectorOAuthFlowService(
        IConnectorRegistry registry,
        IExternalIdentityProviderRepository providerRepository,
        IExternalIdentityBindingService bindingService,
        IConnectorJwtIssuer jwtIssuer,
        IOAuthStateStore stateStore,
        ITenantProvider tenantProvider,
        TimeProvider timeProvider,
        ILogger<ConnectorOAuthFlowService> logger)
    {
        _registry = registry;
        _providerRepository = providerRepository;
        _bindingService = bindingService;
        _jwtIssuer = jwtIssuer;
        _stateStore = stateStore;
        _tenantProvider = tenantProvider;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<OAuthInitiationResponse> InitiateAsync(OAuthInitiationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = _tenantProvider.GetTenantId();
        var provider = await _providerRepository.GetByIdAsync(tenantId, request.ProviderId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessException("CONNECTOR_PROVIDER_NOT_FOUND", $"Provider {request.ProviderId} not found.");
        if (!provider.Enabled)
        {
            throw new BusinessException("CONNECTOR_PROVIDER_DISABLED", $"Provider {request.ProviderId} disabled.");
        }

        var providerType = provider.ProviderType.ToProviderType();
        var identityProvider = _registry.GetIdentity(providerType);
        var redirectUri = BuildRedirectUri(provider.CallbackBaseUrl);

        var stateValue = OAuthState.CreateValue();
        var ttl = TimeSpan.FromMinutes(10);
        var state = new OAuthState
        {
            Value = stateValue,
            TenantId = tenantId.Value,
            ProviderInstanceId = provider.Id,
            ProviderType = providerType,
            RedirectUri = redirectUri,
            PostLoginRedirect = request.PostLoginRedirect,
            ExpiresAt = _timeProvider.GetUtcNow().Add(ttl),
        };
        await _stateStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);

        var context = new ConnectorContext
        {
            TenantId = tenantId.Value,
            ProviderInstanceId = provider.Id,
            ProviderType = providerType,
        };
        var url = identityProvider.BuildAuthorizationUrl(context, redirectUri, stateValue, scopes: null, cancellationToken);
        return new OAuthInitiationResponse
        {
            AuthorizationUrl = url.ToString(),
            State = stateValue,
            ExpiresAt = state.ExpiresAt,
        };
    }

    public async Task<OAuthCallbackResult> CompleteAsync(OAuthCallbackRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var state = await _stateStore.ConsumeAsync(request.State, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessException("CONNECTOR_OAUTH_STATE_INVALID", "OAuth state invalid or expired.");

        var tenantId = _tenantProvider.GetTenantId();
        if (state.TenantId != tenantId.Value)
        {
            _logger.LogWarning("OAuth state tenant mismatch: state.TenantId={StateTenant}, current={Current}", state.TenantId, tenantId.Value);
            throw new BusinessException("CONNECTOR_OAUTH_STATE_INVALID", "OAuth state tenant mismatch.");
        }
        if (state.IsExpired(_timeProvider.GetUtcNow()))
        {
            throw new BusinessException("CONNECTOR_OAUTH_STATE_EXPIRED", "OAuth state expired.");
        }

        var identityProvider = _registry.GetIdentity(state.ProviderType);
        var context = new ConnectorContext
        {
            TenantId = state.TenantId,
            ProviderInstanceId = state.ProviderInstanceId,
            ProviderType = state.ProviderType,
        };

        var profile = await identityProvider.ExchangeCodeAsync(context, request.Code, state.RedirectUri, cancellationToken).ConfigureAwait(false);

        var resolution = await _bindingService.ResolveOrAttemptBindAsync(state.ProviderInstanceId, profile, IdentityBindingMatchStrategy.Mobile, cancellationToken).ConfigureAwait(false);

        var result = new OAuthCallbackResult
        {
            ExternalUserId = profile.ExternalUserId,
            Mobile = profile.Mobile,
            Email = profile.Email,
            DisplayName = profile.Name,
        };

        switch (resolution.Kind)
        {
            case BindingResolutionKind.Existing:
            case BindingResolutionKind.AutoCreated:
                if (resolution.Binding is null)
                {
                    throw new BusinessException("CONNECTOR_BINDING_MISSING", "Binding resolved but payload missing.");
                }
                var token = await _jwtIssuer.IssueAsync(resolution.Binding.LocalUserId, state.ProviderType, cancellationToken).ConfigureAwait(false);
                await _bindingService.TouchLoginAsync(resolution.Binding.Id, cancellationToken).ConfigureAwait(false);
                result.AccessToken = token.AccessToken;
                result.RefreshToken = token.RefreshToken;
                result.ExpiresAt = token.ExpiresAt;
                result.LocalUserId = resolution.Binding.LocalUserId;
                result.RedirectTo = state.PostLoginRedirect ?? "/";
                break;
            case BindingResolutionKind.PendingConfirm:
            case BindingResolutionKind.PendingManual:
                result.PendingBindingTicket = resolution.PendingTicket;
                result.RedirectTo = "/sign?bindingPending=1";
                break;
            case BindingResolutionKind.Conflict:
                result.PendingBindingTicket = resolution.PendingTicket;
                result.RedirectTo = $"/sign?bindingConflict=1&conflictBindingId={resolution.ConflictWith?.Id}";
                break;
        }

        return result;
    }

    private static string BuildRedirectUri(string callbackBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(callbackBaseUrl))
        {
            throw new BusinessException("CONNECTOR_PROVIDER_CONFIG_INVALID", "Provider has no callback base url configured.");
        }
        // 统一约定 OAuth 回调路径：/api/v1/connectors/oauth/callback
        return callbackBaseUrl.TrimEnd('/') + "/api/v1/connectors/oauth/callback";
    }
}
