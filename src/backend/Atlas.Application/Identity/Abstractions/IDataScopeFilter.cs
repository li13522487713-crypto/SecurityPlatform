using Atlas.Core.Enums;

namespace Atlas.Application.Identity.Abstractions;

/// <summary>
/// 数据权限过滤器（等保2.0 访问控制）
/// </summary>
public interface IDataScopeFilter
{
    /// <summary>
    /// 获取当前用户生效的数据权限范围
    /// </summary>
    Task<DataScopeType> GetEffectiveScopeAsync(CancellationToken ct = default);

    /// <summary>
    /// 当权限为 OnlySelf 时，返回当前用户 ID；否则返回 null（不限制）
    /// </summary>
    Task<long?> GetOwnerFilterIdAsync(CancellationToken ct = default);
}
