namespace Atlas.Application.Options;

/// <summary>
/// 数据库初始化器选项，用于控制启动时的初始化行为以加快启动速度。
/// 所有开关默认关闭（跳过），首次部署或数据库变更时需在配置文件中手动将对应项设为 false。
/// </summary>
public sealed class DatabaseInitializerOptions
{
    /// <summary>
    /// 是否跳过数据库 Schema 初始化（CodeFirst.InitTables）。
    /// 默认：true（跳过）；首次部署或实体结构变更后需设为 false。
    /// </summary>
    public bool SkipSchemaInit { get; init; } = true;

    /// <summary>
    /// 是否跳过种子数据初始化（角色、权限、菜单、部门、职位等 Bootstrap 数据）。
    /// 默认：true（跳过）；首次部署或需要补齐缺失基础数据时需设为 false。
    /// </summary>
    public bool SkipSeedData { get; init; } = true;

    /// <summary>
    /// 是否跳过 Schema 迁移检查（Ensure*SchemaAsync 系列，用于修复可空字段等历史兼容问题）。
    /// 默认：true（跳过）；存在旧版本数据库升级场景时需设为 false。
    /// </summary>
    public bool SkipSchemaMigrations { get; init; } = true;
}
