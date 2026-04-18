using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities.Channels;

/// <summary>
/// 治理 M-G02-C5：飞书渠道凭据（per-channel）。
///
/// 一个 <see cref="WorkspacePublishChannel"/>（type=feishu）对应一条凭据。
/// AppSecret / EncryptKey 等敏感值通过 LowCodeCredentialProtector 加密后落库（lcp: 前缀）。
/// TenantAccessToken 缓存仅在内存中维护，DB 只保存到期时间 + 刷新计数，避免冷启动重复触发刷新风暴。
/// </summary>
[SugarTable("FeishuChannelCredential")]
public sealed class FeishuChannelCredential : TenantEntity
{
    public FeishuChannelCredential()
        : base(TenantId.Empty)
    {
        WorkspaceId = string.Empty;
        AppId = string.Empty;
        AppSecretEnc = string.Empty;
        VerificationToken = string.Empty;
        EncryptKeyEnc = string.Empty;
        AgentBindingsJson = "[]";
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        TenantAccessTokenExpiresAt = DateTime.UnixEpoch;
        RefreshCount = 0;
    }

    public FeishuChannelCredential(
        TenantId tenantId,
        long channelId,
        string workspaceId,
        string appId,
        string appSecretEnc,
        string verificationToken,
        string encryptKeyEnc,
        string agentBindingsJson,
        long id)
        : base(tenantId)
    {
        Id = id;
        ChannelId = channelId;
        WorkspaceId = workspaceId;
        AppId = appId;
        AppSecretEnc = appSecretEnc;
        VerificationToken = verificationToken;
        EncryptKeyEnc = encryptKeyEnc;
        AgentBindingsJson = string.IsNullOrWhiteSpace(agentBindingsJson) ? "[]" : agentBindingsJson;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        TenantAccessTokenExpiresAt = DateTime.UnixEpoch;
        RefreshCount = 0;
    }

    public long ChannelId { get; private set; }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string WorkspaceId { get; private set; }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string AppId { get; private set; }

    [SugarColumn(Length = 256, IsNullable = false)]
    public string AppSecretEnc { get; private set; }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string VerificationToken { get; private set; }

    [SugarColumn(Length = 256, IsNullable = false)]
    public string EncryptKeyEnc { get; private set; }

    /// <summary>JSON: [{ agentId, displayName? }]，决定哪些 agent 可被本飞书 bot 路由。</summary>
    [SugarColumn(ColumnDataType = "TEXT", IsNullable = false)]
    public string AgentBindingsJson { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public DateTime TenantAccessTokenExpiresAt { get; private set; }

    public long RefreshCount { get; private set; }

    public void Update(
        string appId,
        string appSecretEnc,
        string verificationToken,
        string encryptKeyEnc,
        string agentBindingsJson)
    {
        AppId = appId;
        AppSecretEnc = appSecretEnc;
        VerificationToken = verificationToken;
        EncryptKeyEnc = encryptKeyEnc;
        AgentBindingsJson = string.IsNullOrWhiteSpace(agentBindingsJson) ? "[]" : agentBindingsJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordTokenRefresh(DateTime expiresAt)
    {
        TenantAccessTokenExpiresAt = expiresAt;
        RefreshCount += 1;
        UpdatedAt = DateTime.UtcNow;
    }
}
