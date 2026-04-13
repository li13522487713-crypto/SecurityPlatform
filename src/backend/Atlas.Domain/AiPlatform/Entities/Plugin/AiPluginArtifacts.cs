using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiPluginPublishRecord : TenantEntity
{
    public AiPluginPublishRecord() : base(TenantId.Empty)
    {
        VersionNumber = string.Empty;
        VersionDescription = string.Empty;
        ConnectorConfigJson = "{}";
        ResultJson = "{}";
    }

    public long PluginId { get; private set; }
    public string VersionNumber { get; private set; }
    public string VersionDescription { get; private set; }
    public string ConnectorConfigJson { get; private set; }
    public string ResultJson { get; private set; }
    public DateTime PublishedAt { get; private set; }
}

public sealed class AiPluginOAuthGrant : TenantEntity
{
    public AiPluginOAuthGrant() : base(TenantId.Empty)
    {
        GrantType = string.Empty;
        EncryptedSecret = string.Empty;
        RefreshToken = string.Empty;
    }

    public long PluginId { get; private set; }
    public long UserId { get; private set; }
    public string GrantType { get; private set; }
    public string EncryptedSecret { get; private set; }
    public string RefreshToken { get; private set; }
    public DateTime? ExpiredAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
}

public sealed class AiPluginDefaultParamBinding : TenantEntity
{
    public AiPluginDefaultParamBinding() : base(TenantId.Empty)
    {
        OwnerType = string.Empty;
        ParamSchemaJson = "{}";
    }

    public long PluginId { get; private set; }
    public long PluginApiId { get; private set; }
    public string OwnerType { get; private set; }
    public long OwnerId { get; private set; }
    public string ParamSchemaJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
}
