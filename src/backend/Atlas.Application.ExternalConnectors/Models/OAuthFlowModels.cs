namespace Atlas.Application.ExternalConnectors.Models;

public sealed class OAuthInitiationRequest
{
    public long ProviderId { get; set; }

    /// <summary>OAuth 完成后落到本地的最终 URL（前端传入；服务端校验是否在 trusted_domains 内）。</summary>
    public string? PostLoginRedirect { get; set; }
}

public sealed class OAuthInitiationResponse
{
    public string AuthorizationUrl { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class OAuthCallbackRequest
{
    public string State { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
}

public sealed class OAuthCallbackResult
{
    /// <summary>若已绑定到本地用户，返回 JWT；否则为 null。</summary>
    public string? AccessToken { get; set; }

    public string? RefreshToken { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>命中的本地用户 ID（已绑定时）。</summary>
    public long? LocalUserId { get; set; }

    /// <summary>未绑定情况下提供给前端"待绑定页"的临时 ticket。</summary>
    public string? PendingBindingTicket { get; set; }

    public string ExternalUserId { get; set; } = string.Empty;

    public string? Mobile { get; set; }

    public string? Email { get; set; }

    public string? DisplayName { get; set; }

    /// <summary>跳转的最终 URL（命中已绑定 → 业务首页；未绑定 → 待绑定页）。</summary>
    public string? RedirectTo { get; set; }
}
