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

    public async Task<LoginLogExportResult> ExportLoginLogsAsync(
        TenantId tenantId,
        string? username,
        string? ipAddress,
        bool? loginStatus,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var items = await _loginLogRepository.QueryAsync(
            tenantId,
            username,
            ipAddress,
            loginStatus,
            from,
            to,
            cancellationToken);

        var builder = new System.Text.StringBuilder();
        builder.AppendLine("用户名,IP地址,浏览器,操作系统,状态,失败原因,登录时间");
        foreach (var item in items)
        {
            var status = item.LoginStatus ? "成功" : "失败";
            builder.AppendLine(string.Join(",",
                EscapeCsv(item.Username),
                EscapeCsv(item.IpAddress),
                EscapeCsv(item.Browser),
                EscapeCsv(item.OperatingSystem),
                EscapeCsv(status),
                EscapeCsv(item.Message),
                EscapeCsv(item.LoginTime.ToString("yyyy-MM-dd HH:mm:ss"))));
        }

        return new LoginLogExportResult(
            $"login-logs-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv",
            "text/csv; charset=utf-8",
            System.Text.Encoding.UTF8.GetBytes(builder.ToString()));
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

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var escaped = value.Replace("\"", "\"\"");
        // RFC 4180: fields containing ", comma, or newline must be enclosed in quotes
        return escaped.Contains('"') || escaped.Contains(',') || escaped.Contains('\n') || escaped.Contains('\r')
            ? $"\"{escaped}\""
            : escaped;
    }
}
