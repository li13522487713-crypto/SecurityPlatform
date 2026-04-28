using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities.Channels;

/// <summary>
/// 微信客服渠道凭据（per-channel）。
/// CorpSecret / EncodingAesKey 等敏感值在落库前加密。
/// </summary>
[SugarTable("WechatCsChannelCredential")]
public sealed class WechatCsChannelCredential : TenantEntity
{
    public WechatCsChannelCredential()
        : base(TenantId.Empty)
    {
        WorkspaceId = string.Empty;
        CorpId = string.Empty;
        CorpSecretEnc = string.Empty;
        OpenKfId = string.Empty;
        Token = string.Empty;
        EncodingAesKeyEnc = string.Empty;
        AgentBindingsJson = "[]";
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        AccessTokenExpiresAt = DateTime.UnixEpoch;
    }

    public WechatCsChannelCredential(
        TenantId tenantId,
        long channelId,
        string workspaceId,
        string corpId,
        string corpSecretEnc,
        string openKfId,
        string token,
        string encodingAesKeyEnc,
        string agentBindingsJson,
        long id)
        : base(tenantId)
    {
        Id = id;
        ChannelId = channelId;
        WorkspaceId = workspaceId;
        CorpId = corpId;
        CorpSecretEnc = corpSecretEnc;
        OpenKfId = openKfId;
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
    public string CorpId { get; private set; }

    [SugarColumn(Length = 256, IsNullable = false)]
    public string CorpSecretEnc { get; private set; }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string OpenKfId { get; private set; }

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
        string corpId,
        string corpSecretEnc,
        string openKfId,
        string token,
        string encodingAesKeyEnc,
        string agentBindingsJson)
    {
        CorpId = corpId;
        CorpSecretEnc = corpSecretEnc;
        OpenKfId = openKfId;
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
