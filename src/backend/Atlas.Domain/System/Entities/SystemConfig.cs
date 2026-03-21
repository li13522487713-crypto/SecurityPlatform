using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.System.Entities;

/// <summary>
/// 系统参数配置（等保2.0：内置参数不可删除，所有修改需审计）
/// </summary>
public sealed class SystemConfig : TenantEntity
{
    public SystemConfig()
        : base(TenantId.Empty)
    {
        ConfigKey = string.Empty;
        ConfigValue = string.Empty;
        ConfigName = string.Empty;
    }

    public SystemConfig(TenantId tenantId, string configKey, string configValue, string configName, bool isBuiltIn, long id)
        : base(tenantId)
    {
        Id = id;
        ConfigKey = configKey;
        ConfigValue = configValue;
        ConfigName = configName;
        IsBuiltIn = isBuiltIn;
    }

    public SystemConfig(TenantId tenantId, string configKey, string configValue, string configName, bool isBuiltIn, long id, string configType)
        : this(tenantId, configKey, configValue, configName, isBuiltIn, id)
    {
        ConfigType = configType;
    }

    public string ConfigKey { get; private set; }
    public string ConfigValue { get; private set; }
    public string ConfigName { get; private set; }

    /// <summary>配置类型：Text=普通字符串, Number=数值, Boolean=布尔, Json=JSON, FeatureFlag=功能开关</summary>
    public string ConfigType { get; private set; } = "Text";

    /// <summary>灰度目标（JSON，用于 FeatureFlag 类型按租户/角色灰度）</summary>
    [SqlSugar.SugarColumn(IsNullable = true)]
    public string? TargetJson { get; private set; }

    /// <summary>内置参数不允许删除（等保2.0：系统基础配置保护）</summary>
    public bool IsBuiltIn { get; private set; }
    public string? Remark { get; private set; }

    public void Update(string configValue, string configName, string? remark)
    {
        ConfigValue = configValue;
        ConfigName = configName;
        Remark = remark;
    }

    public void UpdateFeatureFlag(string enabled, string configName, string? targetJson, string? remark)
    {
        ConfigValue = enabled;
        ConfigName = configName;
        TargetJson = targetJson;
        Remark = remark;
    }
}
