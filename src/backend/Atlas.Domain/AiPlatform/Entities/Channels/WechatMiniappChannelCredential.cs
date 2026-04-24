using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities.Channels;

/// <summary>
/// 微信小程序渠道凭据（per-channel）。
/// AppSecret / EncodingAesKey 等敏感值在落库前加密。
/// </summary>
[SugarTable("WechatMiniappChannelCredential")]
public sealed class WechatMiniappChannelCredential : TenantEntity
{
    public WechatMiniappChannelCredential()
        : base(TenantId.Empty)
    {
        WorkspaceId = string.Empty;
        AppId = string.Empty;
        AppSecretEnc = string.Empty;
        OriginalId = string.Empty;
        MessageToken = string.Empty;
        EncodingAesKeyEnc = string.Empty;
        AgentBindingsJson = "[]";
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        AccessTokenExpiresAt = DateTime.UnixEpoch;
    }

    public WechatMiniappChannelCredential(
        TenantId tenantId,
        long channelId,
        string workspaceId,
        string appId,
        string appSecretEnc,
        string originalId,
        string messageToken,
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
        OriginalId = originalId;
        MessageToken = messageToken;
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
    public string OriginalId { get; private set; }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string MessageToken { get; private set; }

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
        string originalId,
        string messageToken,
        string encodingAesKeyEnc,
        string agentBindingsJson)
    {
        AppId = appId;
        AppSecretEnc = appSecretEnc;
        OriginalId = originalId;
        MessageToken = messageToken;
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
