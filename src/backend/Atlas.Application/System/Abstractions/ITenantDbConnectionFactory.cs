namespace Atlas.Application.System.Abstractions;

/// <summary>
/// 租户数据库连接工厂：根据租户 ID 动态获取连接字符串
/// </summary>
public interface ITenantDbConnectionFactory
{
    /// <summary>
    /// 获取指定租户的数据库连接字符串。
    /// 如未配置自定义数据源，返回 null（调用方应回退到默认连接）。
    /// </summary>
    Task<string?> GetConnectionStringAsync(string tenantId, CancellationToken ct = default);

    /// <summary>使连接字符串缓存失效，用于配置变更时</summary>
    void InvalidateCache(string tenantId);
}
