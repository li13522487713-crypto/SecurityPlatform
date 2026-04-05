using Atlas.Core.Enums;

namespace Atlas.Application.Platform.Abstractions;

/// <summary>
/// 应用级数据权限过滤器（基于 AppRole + AppDepartment，等保2.0 最小化授权）
/// </summary>
public interface IAppDataScopeFilter
{
    Task<DataScopeType> GetEffectiveScopeAsync(long appId, CancellationToken ct = default);

    Task<long?> GetOwnerFilterIdAsync(long appId, CancellationToken ct = default);

    Task<IReadOnlyList<long>?> GetDeptFilterIdsAsync(long appId, CancellationToken ct = default);

    Task<IReadOnlyList<long>?> GetProjectFilterIdsAsync(long appId, CancellationToken ct = default);
}
