using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Tenancy;
using Atlas.Shared.Contracts.Health;
using Atlas.Shared.Contracts.Process;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.PlatformRuntime;

public sealed class FileSystemAppInstanceRegistry : IAppInstanceRegistry
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ILogger<FileSystemAppInstanceRegistry> logger;
    private readonly string instanceRoot;

    public FileSystemAppInstanceRegistry(
        IConfiguration configuration,
        ILogger<FileSystemAppInstanceRegistry> logger)
    {
        this.logger = logger;
        instanceRoot = ResolveInstanceRoot(configuration);
    }

    public Task<TenantAppInstanceRuntimeInfo> EnsureAsync(
        TenantId tenantId,
        TenantAppInstanceRuntimeRegistration registration,
        CancellationToken cancellationToken = default)
    {
        var existing = LoadState(tenantId, long.Parse(registration.AppInstanceId));
        if (existing is not null)
        {
            var normalized = existing with
            {
                AppKey = registration.AppKey,
                CurrentArtifactId = registration.CurrentArtifactId ?? existing.CurrentArtifactId
            };
            return SaveAsync(tenantId, normalized, cancellationToken);
        }

        var instanceHome = GetInstanceHome(tenantId, long.Parse(registration.AppInstanceId), registration.AppKey);
        Directory.CreateDirectory(instanceHome);
        Directory.CreateDirectory(Path.Combine(instanceHome, "artifacts"));
        Directory.CreateDirectory(Path.Combine(instanceHome, "current"));
        Directory.CreateDirectory(Path.Combine(instanceHome, "config"));
        Directory.CreateDirectory(Path.Combine(instanceHome, "logs"));

        var port = ReservePort();
        var ingressUrl = $"http://127.0.0.1:{port}";
        var runtimeInfo = new TenantAppInstanceRuntimeInfo(
            registration.AppInstanceId,
            registration.AppKey,
            RuntimeStates.Stopped,
            HealthStates.Unknown,
            port,
            null,
            registration.CurrentArtifactId ?? $"release-v{registration.Version}",
            ingressUrl,
            $"{ingressUrl}/app-login.html",
            instanceHome,
            Path.Combine(instanceHome, "config", "appinstance.json"),
            null,
            DateTimeOffset.UtcNow.ToString("O"),
            null);

        logger.LogInformation(
            "已为租户应用实例创建目录。TenantId={TenantId}; AppInstanceId={AppInstanceId}; InstanceHome={InstanceHome}; Port={Port}",
            tenantId.Value,
            registration.AppInstanceId,
            instanceHome,
            port);

        return SaveAsync(tenantId, runtimeInfo, cancellationToken);
    }

    public Task<TenantAppInstanceRuntimeInfo?> GetAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LoadState(tenantId, appInstanceId));
    }

    public Task<IReadOnlyDictionary<long, TenantAppInstanceRuntimeInfo>> GetManyAsync(
        TenantId tenantId,
        IReadOnlyCollection<long> appInstanceIds,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<long, TenantAppInstanceRuntimeInfo>();
        foreach (var appInstanceId in appInstanceIds)
        {
            var state = LoadState(tenantId, appInstanceId);
            if (state is not null)
            {
                result[appInstanceId] = state;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<long, TenantAppInstanceRuntimeInfo>>(result);
    }

    public Task<TenantAppInstanceRuntimeInfo> SaveAsync(
        TenantId tenantId,
        TenantAppInstanceRuntimeInfo runtimeInfo,
        CancellationToken cancellationToken = default)
    {
        var appInstanceId = long.Parse(runtimeInfo.InstanceId);
        var instanceHome = runtimeInfo.InstanceHome ?? GetInstanceHome(tenantId, appInstanceId, runtimeInfo.AppKey);
        var configPath = runtimeInfo.ConfigPath ?? Path.Combine(instanceHome, "config", "appinstance.json");
        Directory.CreateDirectory(instanceHome);
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

        var normalizedIngress = string.IsNullOrWhiteSpace(runtimeInfo.IngressUrl)
            ? $"http://127.0.0.1:{runtimeInfo.AssignedPort.GetValueOrDefault()}"
            : runtimeInfo.IngressUrl;
        var normalized = runtimeInfo with
        {
            IngressUrl = normalizedIngress,
            LoginUrl = string.IsNullOrWhiteSpace(runtimeInfo.LoginUrl) ? $"{normalizedIngress}/app-login.html" : runtimeInfo.LoginUrl,
            InstanceHome = instanceHome,
            ConfigPath = configPath
        };

        var statePath = Path.Combine(instanceHome, "runtime-state.json");
        File.WriteAllText(statePath, JsonSerializer.Serialize(normalized, SerializerOptions));

        var instanceConfig = new AppInstanceConfig
        {
            AppKey = normalized.AppKey,
            InstanceId = normalized.InstanceId,
            EnvironmentName = "Development",
            BaseUrl = normalized.IngressUrl ?? string.Empty,
            LoginUrl = normalized.LoginUrl ?? string.Empty,
            Port = normalized.AssignedPort.GetValueOrDefault()
        };
        File.WriteAllText(configPath, JsonSerializer.Serialize(instanceConfig, SerializerOptions));

        return Task.FromResult(normalized);
    }

    private TenantAppInstanceRuntimeInfo? LoadState(TenantId tenantId, long appInstanceId)
    {
        var tenantDirectory = Path.Combine(instanceRoot, $"tenant-{tenantId.Value:D}");
        if (!Directory.Exists(tenantDirectory))
        {
            return null;
        }

        var pattern = $"app-{appInstanceId}-*";
        var instanceDirectory = Directory
            .EnumerateDirectories(tenantDirectory, pattern, SearchOption.TopDirectoryOnly)
            .FirstOrDefault();
        if (instanceDirectory is null)
        {
            return null;
        }

        var statePath = Path.Combine(instanceDirectory, "runtime-state.json");
        if (!File.Exists(statePath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<TenantAppInstanceRuntimeInfo>(File.ReadAllText(statePath), SerializerOptions);
    }

    private string GetInstanceHome(TenantId tenantId, long appInstanceId, string appKey)
    {
        var safeKey = SanitizePathSegment(appKey);
        return Path.Combine(instanceRoot, $"tenant-{tenantId.Value:D}", $"app-{appInstanceId}-{safeKey}");
    }

    private static string ResolveInstanceRoot(IConfiguration configuration)
    {
        var configuredRoot = configuration["Atlas:Runtime:InstanceRoot"];
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            return Path.GetFullPath(configuredRoot);
        }

        var repoRoot = ResolveRepoRoot();
        return Path.Combine(repoRoot, "src", "backend", "Atlas.PlatformHost", "runtime-instances");
    }

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var srcBackend = Path.Combine(directory.FullName, "src", "backend");
            if (Directory.Exists(srcBackend))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static int ReservePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string SanitizePathSegment(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitizedChars = value
            .Select(character => invalidChars.Contains(character) ? '-' : character)
            .ToArray();
        return new string(sanitizedChars).Trim('-', ' ');
    }
}

public sealed class LocalChildProcessManager : IAppProcessManager
{
    private readonly IConfiguration configuration;
    private readonly ILogger<LocalChildProcessManager> logger;
    private readonly string appHostProjectPath;

    public LocalChildProcessManager(
        IConfiguration configuration,
        ILogger<LocalChildProcessManager> logger)
    {
        this.configuration = configuration;
        this.logger = logger;
        appHostProjectPath = ResolveAppHostProjectPath(configuration);
    }

    public Task<TenantAppInstanceRuntimeInfo> StartAsync(
        TenantAppInstanceRuntimeInfo runtimeInfo,
        CancellationToken cancellationToken = default)
    {
        if (runtimeInfo.AssignedPort is null or <= 0)
        {
            throw new InvalidOperationException("AssignedPort 未设置，无法启动 AppHost 实例。");
        }

        if (runtimeInfo.ConfigPath is null)
        {
            throw new InvalidOperationException("ConfigPath 未设置，无法启动 AppHost 实例。");
        }

        if (runtimeInfo.CurrentPid is int existingPid && IsProcessAlive(existingPid))
        {
            return Task.FromResult(runtimeInfo with { RuntimeStatus = RuntimeStates.Running });
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{appHostProjectPath}\" --no-build --no-launch-profile",
            WorkingDirectory = Path.GetDirectoryName(appHostProjectPath)!,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        startInfo.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{runtimeInfo.AssignedPort.Value}";
        startInfo.Environment["AppInstance__ConfigPath"] = runtimeInfo.ConfigPath;
        ForwardEnvironment(startInfo, "Database__Encryption__Key", configuration["Database:Encryption:Key"]);
        ForwardEnvironment(startInfo, "Database__Encryption__Enabled", configuration["Database:Encryption:Enabled"]);

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("无法启动 AppHost 子进程。");
        }

        logger.LogInformation(
            "已拉起 AppHost 子进程。AppInstanceId={AppInstanceId}; Pid={Pid}; Port={Port}",
            runtimeInfo.InstanceId,
            process.Id,
            runtimeInfo.AssignedPort.Value);

        return Task.FromResult(runtimeInfo with
        {
            RuntimeStatus = RuntimeStates.Starting,
            CurrentPid = process.Id,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            StoppedAt = null
        });
    }

    public Task<TenantAppInstanceRuntimeInfo> StopAsync(
        TenantAppInstanceRuntimeInfo runtimeInfo,
        CancellationToken cancellationToken = default)
    {
        if (runtimeInfo.CurrentPid is int pid && IsProcessAlive(pid))
        {
            using var process = Process.GetProcessById(pid);
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);

            logger.LogInformation(
                "已停止 AppHost 子进程。AppInstanceId={AppInstanceId}; Pid={Pid}",
                runtimeInfo.InstanceId,
                pid);
        }

        return Task.FromResult(runtimeInfo with
        {
            RuntimeStatus = RuntimeStates.Stopped,
            HealthStatus = HealthStates.Unknown,
            CurrentPid = null,
            StoppedAt = DateTimeOffset.UtcNow.ToString("O"),
            LastHealthCheckedAt = DateTimeOffset.UtcNow.ToString("O")
        });
    }

    private static string ResolveAppHostProjectPath(IConfiguration configuration)
    {
        var configuredPath = configuration["Atlas:Runtime:AppHostProject"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        var repoRoot = ResolveRepoRoot();
        return Path.Combine(repoRoot, "src", "backend", "Atlas.AppHost", "Atlas.AppHost.csproj");
    }

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var srcBackend = Path.Combine(directory.FullName, "src", "backend");
            if (Directory.Exists(srcBackend))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void ForwardEnvironment(ProcessStartInfo startInfo, string key, string? configuredValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            value = configuredValue;
        }

        if (!string.IsNullOrWhiteSpace(value))
        {
            startInfo.Environment[key] = value;
        }
    }
}

public sealed class HttpAppHealthProbe : IAppHealthProbe
{
    private readonly IHttpClientFactory httpClientFactory;

    public HttpAppHealthProbe(IHttpClientFactory httpClientFactory)
    {
        this.httpClientFactory = httpClientFactory;
    }

    public async Task<TenantAppInstanceHealthInfo> ProbeAsync(
        TenantAppInstanceRuntimeInfo runtimeInfo,
        CancellationToken cancellationToken = default)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(runtimeInfo.IngressUrl))
        {
            return BuildUnknown(runtimeInfo, checkedAt, "IngressUrl 未配置。");
        }

        try
        {
            var client = httpClientFactory.CreateClient("app-runtime-health");
            var readyReport = await client.GetFromJsonAsync<AppHealthReport>(
                $"{runtimeInfo.IngressUrl.TrimEnd('/')}/internal/health/ready-report",
                cancellationToken);

            if (readyReport is not null)
            {
                return new TenantAppInstanceHealthInfo(
                    runtimeInfo.InstanceId,
                    readyReport.Status.ToString(),
                    readyReport.Ready ? HealthStates.Healthy : HealthStates.Unhealthy,
                    readyReport.Live,
                    readyReport.Ready,
                    readyReport.Version,
                    readyReport.Message,
                    checkedAt.ToString("O"),
                    runtimeInfo.IngressUrl);
            }
        }
        catch
        {
        }

        try
        {
            var client = httpClientFactory.CreateClient("app-runtime-health");
            using var liveResponse = await client.GetAsync(
                $"{runtimeInfo.IngressUrl.TrimEnd('/')}/internal/health/live",
                cancellationToken);
            using var readyResponse = await client.GetAsync(
                $"{runtimeInfo.IngressUrl.TrimEnd('/')}/internal/health/ready",
                cancellationToken);

            var live = liveResponse.IsSuccessStatusCode;
            var ready = readyResponse.IsSuccessStatusCode;
            return new TenantAppInstanceHealthInfo(
                runtimeInfo.InstanceId,
                ready ? RuntimeStates.Running : runtimeInfo.RuntimeStatus,
                ready ? HealthStates.Healthy : HealthStates.Unhealthy,
                live,
                ready,
                null,
                ready ? "ready" : "probe-failed",
                checkedAt.ToString("O"),
                runtimeInfo.IngressUrl);
        }
        catch (Exception ex)
        {
            return BuildUnknown(runtimeInfo, checkedAt, ex.Message);
        }
    }

    private static TenantAppInstanceHealthInfo BuildUnknown(
        TenantAppInstanceRuntimeInfo runtimeInfo,
        DateTimeOffset checkedAt,
        string? message)
    {
        return new TenantAppInstanceHealthInfo(
            runtimeInfo.InstanceId,
            runtimeInfo.RuntimeStatus,
            HealthStates.Unknown,
            false,
            false,
            null,
            message,
            checkedAt.ToString("O"),
            runtimeInfo.IngressUrl);
    }
}

public sealed class DefaultAppIngressResolver : IAppIngressResolver
{
    public string ResolveIngressUrl(TenantAppInstanceRuntimeInfo runtimeInfo)
    {
        return runtimeInfo.AssignedPort is > 0
            ? $"http://127.0.0.1:{runtimeInfo.AssignedPort.Value}"
            : string.Empty;
    }
}

public sealed class DefaultAppLoginEntryResolver : IAppLoginEntryResolver
{
    public string ResolveLoginUrl(TenantAppInstanceRuntimeInfo runtimeInfo)
    {
        var ingressUrl = string.IsNullOrWhiteSpace(runtimeInfo.IngressUrl)
            ? $"http://127.0.0.1:{runtimeInfo.AssignedPort.GetValueOrDefault()}"
            : runtimeInfo.IngressUrl;
        return $"{ingressUrl.TrimEnd('/')}/app-login.html";
    }
}

public sealed class AppRuntimeSupervisor : IAppRuntimeSupervisor
{
    private readonly IAppInstanceRegistry registry;
    private readonly IAppProcessManager processManager;
    private readonly IAppHealthProbe healthProbe;
    private readonly IAppIngressResolver ingressResolver;
    private readonly IAppLoginEntryResolver loginEntryResolver;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> locks = new();

    public AppRuntimeSupervisor(
        IAppInstanceRegistry registry,
        IAppProcessManager processManager,
        IAppHealthProbe healthProbe,
        IAppIngressResolver ingressResolver,
        IAppLoginEntryResolver loginEntryResolver)
    {
        this.registry = registry;
        this.processManager = processManager;
        this.healthProbe = healthProbe;
        this.ingressResolver = ingressResolver;
        this.loginEntryResolver = loginEntryResolver;
    }

    public async Task<IReadOnlyDictionary<long, TenantAppInstanceRuntimeInfo>> GetRuntimeSnapshotMapAsync(
        TenantId tenantId,
        IReadOnlyCollection<long> appInstanceIds,
        CancellationToken cancellationToken = default)
    {
        var snapshots = await registry.GetManyAsync(tenantId, appInstanceIds, cancellationToken);
        var result = new Dictionary<long, TenantAppInstanceRuntimeInfo>();
        foreach (var entry in snapshots)
        {
            result[entry.Key] = await RefreshRuntimeInfoAsync(tenantId, entry.Value, probeHealth: false, cancellationToken);
        }

        return result;
    }

    public async Task<TenantAppInstanceRuntimeInfo?> GetRuntimeInfoAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await registry.GetAsync(tenantId, appInstanceId, cancellationToken);
        if (snapshot is null)
        {
            return null;
        }

        return await RefreshRuntimeInfoAsync(tenantId, snapshot, probeHealth: true, cancellationToken);
    }

    public async Task<TenantAppInstanceHealthInfo?> GetHealthAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken = default)
    {
        var runtimeInfo = await GetRuntimeInfoAsync(tenantId, appInstanceId, cancellationToken);
        if (runtimeInfo is null)
        {
            return null;
        }

        return await healthProbe.ProbeAsync(runtimeInfo, cancellationToken);
    }

    public async Task<TenantAppInstanceRuntimeInfo> StartAsync(
        TenantId tenantId,
        TenantAppInstanceRuntimeRegistration registration,
        CancellationToken cancellationToken = default)
    {
        var gate = GetLock(tenantId, registration.AppInstanceId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var runtimeInfo = await registry.EnsureAsync(tenantId, registration, cancellationToken);
            runtimeInfo = await RefreshRuntimeInfoAsync(tenantId, runtimeInfo, probeHealth: false, cancellationToken);
            if (runtimeInfo.RuntimeStatus == RuntimeStates.Running)
            {
                return runtimeInfo;
            }

            var started = await processManager.StartAsync(runtimeInfo, cancellationToken);
            started = started with
            {
                IngressUrl = ingressResolver.ResolveIngressUrl(started),
                LoginUrl = loginEntryResolver.ResolveLoginUrl(started)
            };
            started = await registry.SaveAsync(tenantId, started, cancellationToken);

            var ready = await WaitForReadyAsync(tenantId, started, cancellationToken);
            return await registry.SaveAsync(tenantId, ready, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<TenantAppInstanceRuntimeInfo> StopAsync(
        TenantId tenantId,
        TenantAppInstanceRuntimeRegistration registration,
        CancellationToken cancellationToken = default)
    {
        var gate = GetLock(tenantId, registration.AppInstanceId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var runtimeInfo = await registry.EnsureAsync(tenantId, registration, cancellationToken);
            runtimeInfo = await RefreshRuntimeInfoAsync(tenantId, runtimeInfo, probeHealth: false, cancellationToken);
            var stopped = await processManager.StopAsync(runtimeInfo, cancellationToken);
            return await registry.SaveAsync(tenantId, stopped, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<TenantAppInstanceRuntimeInfo> RestartAsync(
        TenantId tenantId,
        TenantAppInstanceRuntimeRegistration registration,
        CancellationToken cancellationToken = default)
    {
        var gate = GetLock(tenantId, registration.AppInstanceId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var runtimeInfo = await registry.EnsureAsync(tenantId, registration, cancellationToken);
            runtimeInfo = await RefreshRuntimeInfoAsync(tenantId, runtimeInfo, probeHealth: false, cancellationToken);
            var stopped = await processManager.StopAsync(runtimeInfo, cancellationToken);
            stopped = await registry.SaveAsync(tenantId, stopped, cancellationToken);
            var started = await processManager.StartAsync(stopped, cancellationToken);
            started = started with
            {
                IngressUrl = ingressResolver.ResolveIngressUrl(started),
                LoginUrl = loginEntryResolver.ResolveLoginUrl(started)
            };
            started = await registry.SaveAsync(tenantId, started, cancellationToken);
            var ready = await WaitForReadyAsync(tenantId, started, cancellationToken);
            return await registry.SaveAsync(tenantId, ready, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<TenantAppInstanceRuntimeInfo> WaitForReadyAsync(
        TenantId tenantId,
        TenantAppInstanceRuntimeInfo runtimeInfo,
        CancellationToken cancellationToken)
    {
        TenantAppInstanceRuntimeInfo current = runtimeInfo;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            current = await RefreshRuntimeInfoAsync(tenantId, current, probeHealth: true, cancellationToken);
            if (current.RuntimeStatus == RuntimeStates.Running && current.HealthStatus == HealthStates.Healthy)
            {
                return current;
            }
        }

        return current;
    }

    private async Task<TenantAppInstanceRuntimeInfo> RefreshRuntimeInfoAsync(
        TenantId tenantId,
        TenantAppInstanceRuntimeInfo runtimeInfo,
        bool probeHealth,
        CancellationToken cancellationToken)
    {
        var normalized = runtimeInfo with
        {
            IngressUrl = ingressResolver.ResolveIngressUrl(runtimeInfo),
            LoginUrl = loginEntryResolver.ResolveLoginUrl(runtimeInfo)
        };

        if (normalized.CurrentPid is int pid && !IsProcessAlive(pid))
        {
            normalized = normalized with
            {
                RuntimeStatus = RuntimeStates.Stopped,
                HealthStatus = HealthStates.Unknown,
                CurrentPid = null,
                StoppedAt = DateTimeOffset.UtcNow.ToString("O"),
                LastHealthCheckedAt = DateTimeOffset.UtcNow.ToString("O")
            };
            return await registry.SaveAsync(tenantId, normalized, cancellationToken);
        }

        if (normalized.CurrentPid is null)
        {
            return await registry.SaveAsync(tenantId, normalized with
            {
                RuntimeStatus = RuntimeStates.Stopped
            }, cancellationToken);
        }

        if (!probeHealth)
        {
            return normalized.RuntimeStatus == RuntimeStates.Running
                ? normalized
                : await registry.SaveAsync(tenantId, normalized with { RuntimeStatus = RuntimeStates.Running }, cancellationToken);
        }

        var health = await healthProbe.ProbeAsync(normalized, cancellationToken);
        normalized = normalized with
        {
            RuntimeStatus = health.Ready ? RuntimeStates.Running : RuntimeStates.Starting,
            HealthStatus = health.HealthStatus,
            LastHealthCheckedAt = health.CheckedAt
        };

        return await registry.SaveAsync(tenantId, normalized, cancellationToken);
    }

    private SemaphoreSlim GetLock(TenantId tenantId, string appInstanceId)
    {
        var key = $"{tenantId.Value:D}:{appInstanceId}";
        return locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

internal static class RuntimeStates
{
    public const string Starting = "Starting";
    public const string Running = "Running";
    public const string Stopped = "Stopped";
    public const string Failed = "Failed";
}

internal static class HealthStates
{
    public const string Healthy = "Healthy";
    public const string Unhealthy = "Unhealthy";
    public const string Unknown = "Unknown";
}
