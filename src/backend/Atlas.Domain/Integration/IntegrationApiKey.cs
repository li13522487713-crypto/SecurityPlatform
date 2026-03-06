using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Integration;

/// <summary>
/// API Key（供外部系统访问集成 API）
/// </summary>
public sealed class IntegrationApiKey : TenantEntity
{
    public IntegrationApiKey()
        : base(TenantId.Empty)
    {
    }

    public string Name { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
}
