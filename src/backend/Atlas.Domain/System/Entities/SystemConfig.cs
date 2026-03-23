using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.System.Entities;

/// <summary>
/// 系统参数配置（等保2.0：内置参数不可删除，所有修改需审计）
/// </summary>
[SugarIndex(
    "IX_SystemConfig_Tenant_App_Key",
    nameof(TenantIdValue), OrderByType.Asc,
    nameof(AppId), OrderByType.Asc,
    nameof(ConfigKey), OrderByType.Asc,
    false)]
public sealed class SystemConfig : TenantEntity
{
    public SystemConfig()
        : base(TenantId.Empty)
    {
        ConfigKey = string.Empty;
        ConfigValue = string.Empty;
        ConfigName = string.Empty;
    }

    public SystemConfig(
        TenantId tenantId,
        string configKey,
        string configValue,
        string configName,
        bool isBuiltIn,
        long id,
        string configType = "Text",
        string? appId = null,
        string? groupName = null,
        bool isEncrypted = false,
        int version = 0)
        : base(tenantId)
    {
        Id = id;
        ConfigKey = configKey;
        ConfigValue = configValue;
        ConfigName = configName;
        ConfigType = configType;
        AppId = string.IsNullOrWhiteSpace(appId) ? null : appId.Trim();
        GroupName = string.IsNullOrWhiteSpace(groupName) ? null : groupName.Trim();
        IsEncrypted = isEncrypted;
        Version = version;
        IsBuiltIn = isBuiltIn;
    }

    public string ConfigKey { get; private set; }
    public string ConfigValue { get; private set; }
    public string ConfigName { get; private set; }

    /// <summary>配置类型：Text=普通字符串, Number=数值, Boolean=布尔, Json=JSON, FeatureFlag=功能开关</summary>
    public string ConfigType { get; private set; } = "Text";

    /// <summary>灰度目标（JSON，用于 FeatureFlag 类型按租户/角色灰度）</summary>
    [SugarColumn(IsNullable = true)]
    public string? TargetJson { get; private set; }

    /// <summary>应用级配置的应用ID，NULL 表示平台级配置。</summary>
    [SugarColumn(IsNullable = true)]
    public string? AppId { get; private set; }

    /// <summary>配置分组（用于前端分组展示）。</summary>
    [SugarColumn(IsNullable = true)]
    public string? GroupName { get; private set; }

    /// <summary>是否加密存储（敏感配置项）。</summary>
    public bool IsEncrypted { get; private set; }

    /// <summary>配置版本号（并发控制与变更追踪）。</summary>
    public int Version { get; private set; }

    /// <summary>内置参数不允许删除（等保2.0：系统基础配置保护）</summary>
    public bool IsBuiltIn { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? Remark { get; private set; }

    public void Update(
        string configValue,
        string configName,
        string? remark,
        string? groupName = null,
        bool? isEncrypted = null)
    {
        ConfigValue = configValue;
        ConfigName = configName;
        if (groupName is not null)
        {
            GroupName = string.IsNullOrWhiteSpace(groupName) ? null : groupName.Trim();
        }
        if (isEncrypted.HasValue)
        {
            IsEncrypted = isEncrypted.Value;
        }
        Remark = remark;
        Version += 1;
    }

    public void UpdateFeatureFlag(string enabled, string configName, string? targetJson, string? remark)
    {
        ConfigValue = enabled;
        ConfigName = configName;
        TargetJson = targetJson;
        Remark = remark;
        Version += 1;
    }
}
