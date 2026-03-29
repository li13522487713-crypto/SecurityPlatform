using Atlas.Application.System.Models;

namespace Atlas.Application.System.Abstractions;

/// <summary>
/// 应用级数据库连接解析器：根据 tenantId + tenantAppInstanceId 解析应用数据面连接信息。
/// </summary>
public interface IAppDbConnectionResolver
{
    Task<TenantDbConnectionInfo?> ResolveAsync(
        string tenantId,
        long tenantAppInstanceId,
        CancellationToken cancellationToken = default);

    void Invalidate(string tenantId, long tenantAppInstanceId);
}
