using Atlas.Application.License.Abstractions;
using Atlas.Application.License.Models;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.License;

/// <summary>
/// 调试模式授权服务：从配置文件读取固定租户信息，始终返回 Enterprise 永久 Active 状态，
/// 跳过证书上传与机器码校验。仅限开发/测试环境，通过 License:DevMode:Enabled=true 启用。
/// </summary>
public sealed class DevLicenseService : ILicenseService
{
    private readonly LicenseStatusDto _status;
    private readonly ILogger<DevLicenseService> _logger;

    public DevLicenseService(
        IOptions<LicenseDevModeOptions> options,
        ILogger<DevLicenseService> logger)
    {
        _logger = logger;
        var o = options.Value;

        _status = new LicenseStatusDto(
            Status: "Active",
            Edition: o.Edition,
            IsPermanent: true,
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: null,
            RemainingDays: null,
            MachineBound: false,
            MachineMatched: true,
            Features: BuildAllFeaturesEnabled(),
            Limits: BuildNoLimits(),
            TenantId: string.IsNullOrWhiteSpace(o.TenantId) ? null : o.TenantId,
            TenantName: string.IsNullOrWhiteSpace(o.CustomerName) ? null : o.CustomerName);

        // 显著警告：避免开发人员遗忘调试开关在生产上开启
        _logger.LogWarning(
            "[License DevMode] ⚠️ 授权证书校验已禁用（License:DevMode:Enabled=true）。" +
            "当前以 {Edition} 永久授权运行，TenantId={TenantId}，客户={CustomerName}。" +
            "请勿在生产环境启用此模式！",
            o.Edition,
            o.TenantId,
            o.CustomerName);
    }

    public LicenseStatusDto GetCurrentStatus() => _status;

    public bool IsFeatureEnabled(string feature) => true;

    public int GetLimit(string limitKey) => -1;

    public void EnsureWithinLimit(string limitKey, int currentCountIncludingPending)
    {
        // DevMode：不限制任何配额，直接放行
    }

    public Task ReloadAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    private static IReadOnlyDictionary<string, bool> BuildAllFeaturesEnabled() =>
        new Dictionary<string, bool>
        {
            ["lowCode"] = true,
            ["workflow"] = true,
            ["approval"] = true,
            ["alert"] = true,
            ["offlineDeploy"] = true,
            ["multiTenant"] = true,
            ["audit"] = true,
        };

    private static IReadOnlyDictionary<string, int> BuildNoLimits() =>
        new Dictionary<string, int>
        {
            ["maxApps"] = -1,
            ["maxUsers"] = -1,
            ["maxTenants"] = -1,
            ["maxDataSources"] = -1,
            ["auditRetentionDays"] = 365,
        };
}
