namespace Atlas.Domain.ExternalConnectors.Enums;

/// <summary>
/// 4 档绑定策略，对应总报告 28.1 节："外部唯一 ID 直绑 / 手机号匹配 / 邮箱匹配 / 人工审核"。
/// </summary>
public enum IdentityBindingMatchStrategy
{
    /// <summary>外部主键直绑（首次接入历史用户表会用到）。</summary>
    Direct = 1,

    /// <summary>手机号精确匹配。</summary>
    Mobile = 2,

    /// <summary>邮箱精确匹配。</summary>
    Email = 3,

    /// <summary>姓名 + 部门辅助匹配（进入待确认）。</summary>
    NameDept = 4,

    /// <summary>人工审核绑定。</summary>
    Manual = 5,
}
