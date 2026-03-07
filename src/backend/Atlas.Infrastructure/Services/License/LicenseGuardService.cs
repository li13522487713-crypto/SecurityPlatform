using System.Text.Json;
using Atlas.Application.License.Abstractions;
using Atlas.Application.License.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Domain.License;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.License;

/// <summary>
/// 授权门控服务：实现 ILicenseService，缓存当前授权状态并提供功能/限额检查。
/// 注册为 Singleton，激活证书后调用 ReloadAsync 刷新缓存。
/// 通过 IServiceScopeFactory 按需创建短生命周期 Scope 解析 ILicenseRepository，
/// 避免 Singleton 直接捕获 Scoped 依赖（captive dependency）。
/// </summary>
public sealed class LicenseGuardService : ILicenseService
{
    private volatile LicenseStatusDto _currentStatus = LicenseStatusDto.None();
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMachineFingerprintService _fingerprintService;
    private readonly ILogger<LicenseGuardService> _logger;

    public LicenseGuardService(
        IServiceScopeFactory scopeFactory,
        IMachineFingerprintService fingerprintService,
        ILogger<LicenseGuardService> logger)
    {
        _scopeFactory = scopeFactory;
        _fingerprintService = fingerprintService;
        _logger = logger;
    }

    public LicenseStatusDto GetCurrentStatus() => _currentStatus;

    public bool IsFeatureEnabled(string feature)
    {
        var status = _currentStatus;
        if (status.Status != "Active")
            return false;

        return status.Features.TryGetValue(feature, out var enabled) && enabled;
    }

    public int GetLimit(string limitKey)
    {
        var status = _currentStatus;
        ThrowIfLicenseInactive(status);

        return status.Limits.TryGetValue(limitKey, out var limit) ? limit : -1;
    }

    public void EnsureWithinLimit(string limitKey, int currentCountIncludingPending)
    {
        var status = _currentStatus;
        ThrowIfLicenseInactive(status);

        if (!status.Limits.TryGetValue(limitKey, out var limit) || limit < 0)
            return; // 限额不存在或 -1 均表示不限制

        if (currentCountIncludingPending > limit)
        {
            throw new BusinessException(
                $"已达到授权限额：{limitKey} = {limit}，当前计数 {currentCountIncludingPending}",
                ErrorCodes.LicenseLimitExceeded);
        }
    }

    private static void ThrowIfLicenseInactive(LicenseStatusDto status)
    {
        if (status.Status == "Active")
            return;

        var (message, code) = status.Status == "Expired"
            ? ("授权证书已过期，请续签后再操作", ErrorCodes.LicenseExpired)
            : ("授权证书无效或尚未激活，请先导入有效证书", ErrorCodes.LicenseInvalid);

        throw new BusinessException(message, code);
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        var previousStatus = _currentStatus;
        var lockAcquired = false;

        try
        {
            await _reloadLock.WaitAsync(cancellationToken);
            lockAcquired = true;

            await using var scope = _scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<ILicenseRepository>();
            var record = await repository.GetActiveAsync(cancellationToken);
            if (record is null)
            {
                _currentStatus = LicenseStatusDto.None();
                _logger.LogInformation("未找到有效授权证书");
                return;
            }

            var now = DateTimeOffset.UtcNow;

            // 检查是否过期
            if (!record.IsPermanent && record.ExpiresAt.HasValue && now > record.ExpiresAt.Value)
            {
                _currentStatus = BuildExpiredStatus(record);
                _logger.LogWarning("授权证书已过期：{ExpiresAt}", record.ExpiresAt);
                return;
            }

            var machineMatched = _fingerprintService.Matches(record.MachineFingerprintHash);
            var remainingDays = CalculateRemainingDays(record, now);
            var features = MergeFeatures(record.Edition, record.FeaturesJson);
            var limits = MergeLimits(record.Edition, record.LimitsJson);

            var tenantId = Guid.TryParse(record.CustomerId, out _) ? record.CustomerId : null;
            _currentStatus = new LicenseStatusDto(
                "Active",
                record.Edition.ToString(),
                record.IsPermanent,
                record.IssuedAt,
                record.ExpiresAt,
                remainingDays,
                !string.IsNullOrWhiteSpace(record.MachineFingerprintHash),
                machineMatched,
                features,
                limits,
                tenantId,
                string.IsNullOrWhiteSpace(record.CustomerName) ? null : record.CustomerName);

            _logger.LogInformation("授权证书加载成功：{Edition}，{Status}",
                record.Edition, record.IsPermanent ? "永久" : $"到期日：{record.ExpiresAt:yyyy-MM-dd}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // 刷新失败时保留当前内存状态，避免瞬时故障导致已激活平台被误判为未激活。
            _currentStatus = previousStatus;
            _logger.LogError(ex, "加载授权证书失败，已保留上次授权状态：{Status}", previousStatus.Status);
        }
        finally
        {
            if (lockAcquired)
            {
                _reloadLock.Release();
            }
        }
    }

    private static LicenseStatusDto BuildExpiredStatus(LicenseRecord record)
    {
        var tenantId = Guid.TryParse(record.CustomerId, out _) ? record.CustomerId : null;
        return new(
            "Expired",
            record.Edition.ToString(),
            record.IsPermanent,
            record.IssuedAt,
            record.ExpiresAt,
            0,
            !string.IsNullOrWhiteSpace(record.MachineFingerprintHash),
            false,
            new Dictionary<string, bool>(),
            new Dictionary<string, int>(),
            tenantId,
            string.IsNullOrWhiteSpace(record.CustomerName) ? null : record.CustomerName);
    }

    private static int? CalculateRemainingDays(LicenseRecord record, DateTimeOffset now)
    {
        if (record.IsPermanent || !record.ExpiresAt.HasValue)
            return null;

        var remaining = (int)(record.ExpiresAt.Value - now).TotalDays;
        return Math.Max(0, remaining);
    }

    /// <summary>
    /// 以版本默认功能开关为基础，用证书 payload 中的自定义值覆盖。
    /// payload 为空时退化为纯版本默认值。
    /// </summary>
    private static IReadOnlyDictionary<string, bool> MergeFeatures(LicenseEdition edition, string featuresJson)
    {
        var defaults = BuildDefaultFeatures(edition);
        if (string.IsNullOrEmpty(featuresJson))
            return defaults;

        try
        {
            var overrides = JsonSerializer.Deserialize<Dictionary<string, bool>>(featuresJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (overrides is null || overrides.Count == 0)
                return defaults;

            var merged = new Dictionary<string, bool>(defaults);
            foreach (var (key, value) in overrides)
                merged[key] = value;
            return merged;
        }
        catch
        {
            return defaults;
        }
    }

    /// <summary>
    /// 以版本默认限额为基础，用证书 payload 中的自定义值覆盖。
    /// payload 为空时退化为纯版本默认值。
    /// </summary>
    private static IReadOnlyDictionary<string, int> MergeLimits(LicenseEdition edition, string limitsJson)
    {
        var defaults = BuildDefaultLimits(edition);
        if (string.IsNullOrEmpty(limitsJson))
            return defaults;

        try
        {
            var overrides = JsonSerializer.Deserialize<Dictionary<string, int>>(limitsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (overrides is null || overrides.Count == 0)
                return defaults;

            var merged = new Dictionary<string, int>(defaults);
            foreach (var (key, value) in overrides)
                merged[key] = value;
            return merged;
        }
        catch
        {
            return defaults;
        }
    }

    private static Dictionary<string, bool> BuildDefaultFeatures(LicenseEdition edition)
    {
        return edition switch
        {
            LicenseEdition.Trial => new Dictionary<string, bool>
            {
                ["lowCode"] = true,
                ["workflow"] = false,
                ["approval"] = false,
                ["alert"] = false,
                ["offlineDeploy"] = false,
                ["multiTenant"] = false,
                ["audit"] = true,
            },
            LicenseEdition.Pro => new Dictionary<string, bool>
            {
                ["lowCode"] = true,
                ["workflow"] = true,
                ["approval"] = true,
                ["alert"] = true,
                ["offlineDeploy"] = true,
                ["multiTenant"] = true,
                ["audit"] = true,
            },
            LicenseEdition.Enterprise => new Dictionary<string, bool>
            {
                ["lowCode"] = true,
                ["workflow"] = true,
                ["approval"] = true,
                ["alert"] = true,
                ["offlineDeploy"] = true,
                ["multiTenant"] = true,
                ["audit"] = true,
            },
            _ => new Dictionary<string, bool>()
        };
    }

    private static Dictionary<string, int> BuildDefaultLimits(LicenseEdition edition)
    {
        return edition switch
        {
            LicenseEdition.Trial => new Dictionary<string, int>
            {
                ["maxApps"] = 3,
                ["maxUsers"] = 10,
                ["maxTenants"] = 1,
                ["maxDataSources"] = 2,
                ["auditRetentionDays"] = 7,
            },
            LicenseEdition.Pro => new Dictionary<string, int>
            {
                ["maxApps"] = 20,
                ["maxUsers"] = 500,
                ["maxTenants"] = 5,
                ["maxDataSources"] = 10,
                ["auditRetentionDays"] = 180,
            },
            LicenseEdition.Enterprise => new Dictionary<string, int>
            {
                ["maxApps"] = -1,
                ["maxUsers"] = -1,
                ["maxTenants"] = -1,
                ["maxDataSources"] = -1,
                ["auditRetentionDays"] = 365,
            },
            _ => new Dictionary<string, int>()
        };
    }
}
