using System.Collections.Concurrent;
using Atlas.Application.Platform.Abstractions;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace Atlas.PlatformHost.ReverseProxy;

/// <summary>
/// 从 IAppInstanceRegistry 读取运行中的实例，动态生成 YARP 路由和集群配置。
/// 路由规则：/app-host/{appKey}/{**remainder} → http://127.0.0.1:{port}/{remainder}
/// </summary>
public sealed class AppHostProxyConfigProvider : IProxyConfigProvider, IDisposable
{
    private readonly IAppInstanceRegistry registry;
    private readonly ILogger<AppHostProxyConfigProvider> logger;
    private readonly CancellationTokenSource cts = new();
    private readonly object syncLock = new();

    private volatile AppHostProxySnapshot currentSnapshot;

    public AppHostProxyConfigProvider(
        IAppInstanceRegistry registry,
        ILogger<AppHostProxyConfigProvider> logger)
    {
        this.registry = registry;
        this.logger = logger;
        currentSnapshot = new AppHostProxySnapshot([], [], new CancellationChangeToken(cts.Token));

        _ = Task.Run(PollLoopAsync);
    }

    public IProxyConfig GetConfig() => currentSnapshot;

    public void Dispose()
    {
        cts.Cancel();
        cts.Dispose();
    }

    private async Task PollLoopAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));

        while (!cts.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "刷新 YARP 代理配置失败");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(cts.Token))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RefreshAsync()
    {
        var instances = await registry.GetAllRunningAsync(cts.Token);

        var routes = new List<RouteConfig>();
        var clusters = new List<ClusterConfig>();
        var seen = new HashSet<string>();

        foreach (var (_, runtimeInfo) in instances)
        {
            if (string.IsNullOrWhiteSpace(runtimeInfo.AppKey) ||
                runtimeInfo.AssignedPort is not > 0 ||
                !seen.Add(runtimeInfo.AppKey))
            {
                continue;
            }

            var clusterId = $"apphost-{runtimeInfo.AppKey}";
            var routeId = $"route-apphost-{runtimeInfo.AppKey}";
            var destination = $"http://127.0.0.1:{runtimeInfo.AssignedPort.Value}";

            clusters.Add(new ClusterConfig
            {
                ClusterId = clusterId,
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["primary"] = new DestinationConfig { Address = destination }
                }
            });

            routes.Add(new RouteConfig
            {
                RouteId = routeId,
                ClusterId = clusterId,
                Match = new RouteMatch
                {
                    Path = $"/app-host/{runtimeInfo.AppKey}/{{**remainder}}"
                },
                Transforms =
                [
                    new Dictionary<string, string> { ["PathRemovePrefix"] = $"/app-host/{runtimeInfo.AppKey}" }
                ]
            });
        }

        lock (syncLock)
        {
            var oldSnapshot = currentSnapshot;
            var newCts = new CancellationTokenSource();
            currentSnapshot = new AppHostProxySnapshot(routes, clusters, new CancellationChangeToken(newCts.Token));

            oldSnapshot.SignalChange();
        }

        logger.LogDebug("YARP 代理配置已刷新，路由数={RouteCount}，集群数={ClusterCount}", routes.Count, clusters.Count);
    }
}

internal sealed class AppHostProxySnapshot : IProxyConfig
{
    private readonly CancellationTokenSource changeCts = new();

    public AppHostProxySnapshot(
        IReadOnlyList<RouteConfig> routes,
        IReadOnlyList<ClusterConfig> clusters,
        IChangeToken changeToken)
    {
        Routes = routes;
        Clusters = clusters;
        ChangeToken = changeToken;
    }

    public IReadOnlyList<RouteConfig> Routes { get; }
    public IReadOnlyList<ClusterConfig> Clusters { get; }
    public IChangeToken ChangeToken { get; }

    public void SignalChange()
    {
        changeCts.Cancel();
    }
}
