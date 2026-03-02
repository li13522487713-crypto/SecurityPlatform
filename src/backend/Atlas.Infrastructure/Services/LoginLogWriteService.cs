using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.System.Entities;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 登录日志写入（等保2.0：日志写入失败不阻断登录，使用 best-effort 模式）
/// </summary>
public sealed class LoginLogWriteService : ILoginLogWriteService
{
    private readonly LoginLogRepository _repository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LoginLogWriteService> _logger;

    public LoginLogWriteService(
        LoginLogRepository repository,
        IIdGeneratorAccessor idGeneratorAccessor,
        TimeProvider timeProvider,
        ILogger<LoginLogWriteService> logger)
    {
        _repository = repository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task WriteAsync(TenantId tenantId, LoginLogWriteRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var (browser, os) = ParseUserAgent(request.UserAgent);
            var log = new LoginLog(
                tenantId,
                request.Username,
                request.IpAddress,
                browser,
                os,
                request.LoginStatus,
                request.Message,
                request.LoginTime,
                _idGeneratorAccessor.NextId());

            await _repository.AddAsync(log, cancellationToken);
        }
        catch (Exception ex)
        {
            // 等保2.0：日志写入失败记录至应用日志，不抛出异常（不阻断登录流程）
            _logger.LogWarning(ex, "登录日志写入失败，用户={Username}，状态={Status}", request.Username, request.LoginStatus);
        }
    }

    private static (string? Browser, string? Os) ParseUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return (null, null);

        string? browser = null;
        string? os = null;

        if (userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
            browser = "Chrome";
        else if (userAgent.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
            browser = "Firefox";
        else if (userAgent.Contains("Safari", StringComparison.OrdinalIgnoreCase))
            browser = "Safari";
        else if (userAgent.Contains("Edge", StringComparison.OrdinalIgnoreCase))
            browser = "Edge";
        else if (userAgent.Contains("MSIE", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("Trident", StringComparison.OrdinalIgnoreCase))
            browser = "IE";
        else
            browser = "Unknown";

        if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase))
            os = "Windows";
        else if (userAgent.Contains("Mac OS", StringComparison.OrdinalIgnoreCase))
            os = "macOS";
        else if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase))
            os = "Linux";
        else if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
            os = "Android";
        else if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase))
            os = "iOS";
        else
            os = "Unknown";

        return (browser, os);
    }
}
