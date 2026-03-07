using Atlas.Application.License.Models;

namespace Atlas.Application.License.Abstractions;

/// <summary>
/// 授权服务接口：查询当前授权状态、进行功能/限额门控。
/// 由 Infrastructure 实现，注册为 Singleton（内存缓存授权状态）。
/// </summary>
public interface ILicenseService
{
    /// <summary>获取当前授权状态（内存缓存，激活后自动刷新）</summary>
    LicenseStatusDto GetCurrentStatus();

    /// <summary>指定功能是否已授权</summary>
    bool IsFeatureEnabled(string feature);

    /// <summary>获取指定限额（-1 表示不限）</summary>
    int GetLimit(string limitKey);

    /// <summary>
    /// 校验当前数量（包含本次新增后的数量）是否超出限额；超出时抛出 BusinessException。
    /// </summary>
    void EnsureWithinLimit(string limitKey, int currentCountIncludingPending);

    /// <summary>激活或刷新后重新加载授权状态</summary>
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
