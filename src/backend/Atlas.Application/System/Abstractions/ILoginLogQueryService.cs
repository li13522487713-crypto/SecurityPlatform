using Atlas.Application.System.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.System.Abstractions;

public interface ILoginLogQueryService
{
    Task<PagedResult<LoginLogDto>> GetLoginLogsPagedAsync(
        TenantId tenantId,
        string? username,
        string? ipAddress,
        bool? loginStatus,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PagedResult<OnlineUserDto>> GetOnlineUsersPagedAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);
}
