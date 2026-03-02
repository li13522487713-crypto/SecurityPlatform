using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Atlas.Infrastructure.Repositories;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class LoginLogQueryService : ILoginLogQueryService
{
    private readonly LoginLogRepository _loginLogRepository;
    private readonly ISqlSugarClient _db;
    private readonly TimeProvider _timeProvider;

    public LoginLogQueryService(LoginLogRepository loginLogRepository, ISqlSugarClient db, TimeProvider timeProvider)
    {
        _loginLogRepository = loginLogRepository;
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<PagedResult<LoginLogDto>> GetLoginLogsPagedAsync(
        TenantId tenantId,
        string? username,
        string? ipAddress,
        bool? loginStatus,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _loginLogRepository.GetPagedAsync(
            tenantId, username, ipAddress, loginStatus, from, to, pageIndex, pageSize, cancellationToken);

        var dtos = items.Select(x => new LoginLogDto(
            x.Id, x.Username, x.IpAddress, x.Browser, x.OperatingSystem,
            x.LoginStatus, x.Message, x.LoginTime)).ToList();

        return new PagedResult<LoginLogDto>(dtos, total, pageIndex, pageSize);
    }

    public async Task<PagedResult<OnlineUserDto>> GetOnlineUsersPagedAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        var query = _db.Queryable<AuthSession>()
            .LeftJoin<UserAccount>((s, u) => s.UserId == u.Id && u.TenantIdValue == tenantId.Value)
            .Where((s, u) => s.TenantIdValue == tenantId.Value
                && s.RevokedAt == null
                && s.ExpiresAt > now);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where((s, u) => u.Username.Contains(keyword) || s.IpAddress.Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy((s, u) => s.LastSeenAt, OrderByType.Desc)
            .Select((s, u) => new
            {
                s.Id,
                s.UserId,
                Username = u.Username,
                s.IpAddress,
                s.ClientType,
                LoginTime = s.CreatedAt,
                s.LastSeenAt,
                s.ExpiresAt
            })
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        var dtos = items.Select(x => new OnlineUserDto(
            x.Id, x.UserId, x.Username, x.IpAddress, x.ClientType,
            x.LoginTime, x.LastSeenAt, x.ExpiresAt)).ToList();

        return new PagedResult<OnlineUserDto>(dtos, total, pageIndex, pageSize);
    }
}
