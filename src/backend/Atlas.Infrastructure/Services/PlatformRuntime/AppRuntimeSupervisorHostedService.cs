using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.PlatformRuntime;

/// <summary>
/// 5s 轮询所有运行中的 AppHost 实例健康状态，失败 3 次停止自动重启并写 LastExitCode。
/// 退避策略：连续探活失败 → 1×5s → 2×5s → 3×5s → 停止自动重启。
/// </summary>
public sealed class AppRuntimeSupervisorHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int MaxAutoRestartAttempts = 3;

    private static readonly TimeSpan DisabledCheckInterval = TimeSpan.FromSeconds(30);

    private readonly IAppInstanceRegistry registry;
    private readonly IAppProcessManager processManager;
    private readonly IAppHealthProbe healthProbe;
    private readonly IAppIngressResolver ingressResolver;
    private readonly IAppLoginEntryResolver loginEntryResolver;
    private readonly IConfiguration configuration;
    private readonly ILogger<AppRuntimeSupervisorHostedService> logger;

    private readonly Dictionary<string, int> failureCounters = new();

    public AppRuntimeSupervisorHostedService(
        IAppInstanceRegistry registry,
        IAppProcessManager processManager,
        IAppHealthProbe healthProbe,
        IAppIngressResolver ingressResolver,
        IAppLoginEntryResolver loginEntryResolver,
        IConfiguration configuration,
        ILogger<AppRuntimeSupervisorHostedService> logger)
    {
        this.registry = registry;
        this.processManager = processManager;
        this.healthProbe = healthProbe;
        this.ingressResolver = ingressResolver;
        this.loginEntryResolver = loginEntryResolver;
        this.configuration = configuration;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = configuration.GetValue("Atlas:Runtime:SupervisorEnabled", true);
        if (!enabled)
        {
            logger.LogInformation("AppRuntimeSupervisorHostedService 已禁用（Atlas:Runtime:SupervisorEnabled=false）");
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // expected
            }
            return;
        }

        logger.LogInformation("AppRuntimeSupervisorHostedService 已启动，轮询间隔 {Interval}s", PollInterval.TotalSeconds);

        using var timer = new PeriodicTimer(PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAllInstancesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AppRuntimeSupervisor 轮询异常");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("AppRuntimeSupervisorHostedService 已停止");
    }

    private async Task CheckAllInstancesAsync(CancellationToken cancellationToken)
    {
        var instances = await registry.GetAllRunningAsync(cancellationToken);
        if (instances.Count == 0)
        {
            return;
        }

        foreach (var (tenantId, runtimeInfo) in instances)
        {
            await CheckInstanceAsync(tenantId, runtimeInfo, cancellationToken);
        }
    }

    private async Task CheckInstanceAsync(
        TenantId tenantId,
        TenantAppInstanceRuntimeInfo runtimeInfo,
        CancellationToken cancellationToken)
    {
        var instanceKey = $"{tenantId.Value:D}:{runtimeInfo.InstanceId}";

        try
        {
            var health = await healthProbe.ProbeAsync(runtimeInfo, cancellationToken);

            if (health.Ready)
            {
                failureCounters.Remove(instanceKey);

                var updated = runtimeInfo with
                {
                    RuntimeStatus = RuntimeStates.Running,
                    HealthStatus = health.HealthStatus,
                    LastHealthCheckedAt = health.CheckedAt
                };
                await registry.SaveAsync(tenantId, updated, cancellationToken);
                return;
            }

            await HandleUnhealthyInstanceAsync(tenantId, runtimeInfo, instanceKey, health.HealthStatus, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "探活实例 {InstanceKey} 时发生异常", instanceKey);
            await HandleUnhealthyInstanceAsync(tenantId, runtimeInfo, instanceKey, HealthStates.Unknown, cancellationToken);
        }
    }

    private async Task HandleUnhealthyInstanceAsync(
        TenantId tenantId,
        TenantAppInstanceRuntimeInfo runtimeInfo,
        string instanceKey,
        string healthStatus,
        CancellationToken cancellationToken)
    {
        failureCounters.TryGetValue(instanceKey, out var failures);
        failures++;
        failureCounters[instanceKey] = failures;

        logger.LogWarning(
            "实例 {InstanceKey} 连续失败 {Failures}/{MaxAttempts}",
            instanceKey, failures, MaxAutoRestartAttempts);

        var updated = runtimeInfo with
        {
            HealthStatus = healthStatus,
            LastHealthCheckedAt = DateTimeOffset.UtcNow.ToString("O")
        };

        if (failures >= MaxAutoRestartAttempts)
        {
            logger.LogError(
                "实例 {InstanceKey} 连续 {Failures} 次探活失败，停止自动重启",
                instanceKey, failures);

            updated = updated with
            {
                RuntimeStatus = RuntimeStates.Failed,
                LastExitCode = -1
            };
            await registry.SaveAsync(tenantId, updated, cancellationToken);
            return;
        }

        logger.LogInformation(
            "尝试自动重启实例 {InstanceKey}（第 {Attempt} 次）",
            instanceKey, failures);

        try
        {
            var stopped = await processManager.StopAsync(updated, cancellationToken);
            var started = await processManager.StartAsync(stopped, cancellationToken);
            started = started with
            {
                IngressUrl = ingressResolver.ResolveIngressUrl(started),
                LoginUrl = loginEntryResolver.ResolveLoginUrl(started)
            };
            await registry.SaveAsync(tenantId, started, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "自动重启实例 {InstanceKey} 失败", instanceKey);
            updated = updated with { RuntimeStatus = RuntimeStates.Failed };
            await registry.SaveAsync(tenantId, updated, cancellationToken);
        }
    }
}
