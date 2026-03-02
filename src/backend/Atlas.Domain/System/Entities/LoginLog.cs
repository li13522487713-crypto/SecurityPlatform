using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.System.Entities;

/// <summary>
/// 登录日志（等保2.0：须记录用户登录成功/失败事件，至少保留6个月）
/// </summary>
public sealed class LoginLog : TenantEntity
{
    public LoginLog()
        : base(TenantId.Empty)
    {
        Username = string.Empty;
        IpAddress = string.Empty;
        LoginStatus = false;
    }

    public LoginLog(
        TenantId tenantId,
        string username,
        string ipAddress,
        string? browser,
        string? operatingSystem,
        bool loginStatus,
        string? message,
        DateTimeOffset loginTime,
        long id)
        : base(tenantId)
    {
        Id = id;
        Username = username;
        IpAddress = ipAddress;
        Browser = browser;
        OperatingSystem = operatingSystem;
        LoginStatus = loginStatus;
        Message = message;
        LoginTime = loginTime;
    }

    public string Username { get; private set; }
    public string IpAddress { get; private set; }
    public string? Browser { get; private set; }
    public string? OperatingSystem { get; private set; }

    /// <summary>true=成功, false=失败</summary>
    public bool LoginStatus { get; private set; }

    public string? Message { get; private set; }
    public DateTimeOffset LoginTime { get; private set; }
}
