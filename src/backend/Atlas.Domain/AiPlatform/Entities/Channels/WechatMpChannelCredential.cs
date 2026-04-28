using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities.Channels;

/// <summary>
/// 治理 M-G02-C9：微信公众号渠道凭据（per-channel）。
/// AppId / AppSecret / Token / EncodingAesKey 来自微信公众平台「基本配置」。
/// 敏感字段（AppSecret / EncodingAesKey）经 LowCodeCredentialProtector 加密落库。
/// </summary>
[SugarTable("WechatMpChannelCredential")]
public sealed class WechatMpChannelCredential : TenantEntity
{
    public WechatMpChannelCredential()
        : base(TenantId.Empty)
    {
        WorkspaceId = string.Empty;
        AppId = string.Empty;
        AppSecretEnc = string.Empty;
        Token = string.Empty;
        EncodingAesKeyEnc = string.Empty;
        AgentBindingsJson = "[]";
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        AccessTokenExpiresAt = DateTime.UnixEpoch;
    }

    public WechatMpChannelCredential(
        TenantId tenantId,
        long channelId,
        string workspaceId,
        string appId,
        string appSecretEnc,
        string token,
        string encodingAesKeyEnc,
        string agentBindingsJson,
        long id)
        : base(tenantId)
    {
        Id = id;
        ChannelId = channelId;
        WorkspaceId = workspaceId;
        AppId = appId;
        AppSecretEnc = appSecretEnc;
        Token = token;
        EncodingAesKeyEnc = encodingAesKeyEnc;
        AgentBindingsJson = string.IsNullOrWhiteSpace(agentBindingsJson) ? "[]" : agentBindingsJson;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        AccessTokenExpiresAt = DateTime.UnixEpoch;
    }

    public long ChannelId { get; private set; }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string WorkspaceId { get; private set; }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string AppId { get; private set; }

    [SugarColumn(Length = 256, IsNullable = false)]
    public string AppSecretEnc { get; private set; }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string Token { get; private set; }

    [SugarColumn(Length = 256, IsNullable = false)]
    public string EncodingAesKeyEnc { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = false)]
    public string AgentBindingsJson { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime AccessTokenExpiresAt { get; private set; }
    public long RefreshCount { get; private set; }

    public void Update(
        string appId,
        string appSecretEnc,
        string token,
        string encodingAesKeyEnc,
        string agentBindingsJson)
    {
        AppId = appId;
        AppSecretEnc = appSecretEnc;
        Token = token;
        EncodingAesKeyEnc = encodingAesKeyEnc;
        AgentBindingsJson = string.IsNullOrWhiteSpace(agentBindingsJson) ? "[]" : agentBindingsJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordAccessTokenRefresh(DateTime expiresAt)
    {
        AccessTokenExpiresAt = expiresAt;
        RefreshCount += 1;
        UpdatedAt = DateTime.UtcNow;
    }
}
