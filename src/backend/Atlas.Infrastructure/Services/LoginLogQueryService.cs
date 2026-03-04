using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Core.Utilities;
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
        const int ExportPageSize = 1000;
        const int MaxExportRows = 10_000;

        var builder = new System.Text.StringBuilder();
        builder.AppendLine("用户名,IP地址,浏览器,操作系统,状态,失败原因,登录时间");

        var pageIndex = 1;
        var totalFetched = 0;

        while (totalFetched < MaxExportRows)
        {
            var (items, _) = await _loginLogRepository.GetPagedAsync(
                tenantId,
                username,
                ipAddress,
                loginStatus,
                from,
                to,
                pageIndex,
                ExportPageSize,
                cancellationToken);

            foreach (var item in items)
            {
                if (totalFetched >= MaxExportRows)
                {
                    break;
                }

                var status = item.LoginStatus ? "成功" : "失败";
                builder.AppendLine(string.Join(",",
                    CsvUtility.EscapeField(item.Username),
                    CsvUtility.EscapeField(item.IpAddress),
                    CsvUtility.EscapeField(item.Browser),
                    CsvUtility.EscapeField(item.OperatingSystem),
                    CsvUtility.EscapeField(status),
                    CsvUtility.EscapeField(item.Message),
                    CsvUtility.EscapeField(item.LoginTime.ToString("yyyy-MM-dd HH:mm:ss"))));
                totalFetched++;
            }

            if (items.Count < ExportPageSize || totalFetched >= MaxExportRows)
            {
                break;
            }

            pageIndex++;
        }

        var csvContent = builder.ToString();
        return new LoginLogExportResult(
            $"login-logs-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv",
            "text/csv; charset=utf-8",
            CsvUtility.GetUtf8BytesWithBom(csvContent));
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
