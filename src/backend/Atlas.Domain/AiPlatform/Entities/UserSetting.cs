using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

/// <summary>
/// 用户级 KV 设置（PRD 03 头像入口）。
///
/// 一行 = 一个用户的一项设置，按 (TenantId, UserId, SettingKey) 三键唯一。
/// 第一阶段承载 Coze 个人设置（general / publish-channels / datasources），
/// 后续可扩展通用偏好（IDE 主题、布局快照等）。
/// </summary>
[SugarTable("UserSetting")]
public sealed class UserSetting : TenantEntity
{
    public UserSetting()
        : base(TenantId.Empty)
    {
        SettingKey = string.Empty;
        ValueJson = "{}";
        CreatedAt = DateTime.UtcNow;
    }

    public UserSetting(
        TenantId tenantId,
        long userId,
        string settingKey,
        string valueJson,
        long id)
        : base(tenantId)
    {
        Id = id;
        UserId = userId;
        SettingKey = settingKey;
        ValueJson = string.IsNullOrWhiteSpace(valueJson) ? "{}" : valueJson;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long UserId { get; private set; }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string SettingKey { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = false)]
    public string ValueJson { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void UpdateValue(string valueJson)
    {
        ValueJson = string.IsNullOrWhiteSpace(valueJson) ? "{}" : valueJson;
        UpdatedAt = DateTime.UtcNow;
    }
}
