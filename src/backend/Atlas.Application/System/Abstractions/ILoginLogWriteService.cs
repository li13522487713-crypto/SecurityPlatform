using Atlas.Application.System.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.System.Abstractions;

/// <summary>
/// 登录日志写入服务（等保2.0：须记录每次登录事件，写入失败不阻断登录流程）
/// </summary>
public interface ILoginLogWriteService
{
    Task WriteAsync(TenantId tenantId, LoginLogWriteRequest request, CancellationToken cancellationToken);
}
