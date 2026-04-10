using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Application.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Atlas.Domain.LowCode.Entities;
using Atlas.Domain.Platform.Entities;
using Atlas.Infrastructure.Caching;
using SqlSugar;

namespace Atlas.Infrastructure.Services.Platform;

public sealed class NavigationProjectionService : INavigationProjectionService
{
    private readonly ISqlSugarClient _db;
    private readonly ICapabilityRegistry _capabilityRegistry;
    private readonly IAtlasHybridCache _cache;

    public NavigationProjectionService(
        ISqlSugarClient db,
        ICapabilityRegistry capabilityRegistry,
        IAtlasHybridCache cache)
    {
        _db = db;
        _capabilityRegistry = capabilityRegistry;
        _cache = cache;
    }

    public async Task<NavigationProjectionResponse> GetPlatformProjectionAsync(
        TenantId tenantId,
        long userId,
        bool isPlatformAdmin,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"nav:platform:{tenantId.Value:D}:{userId}:{isPlatformAdmin}";
        var cached = await _cache.TryGetAsync<NavigationProjectionResponse>(cacheKey, cancellationToken: cancellationToken);
        if (cached.Found && cached.Value is not null)
        {
            return cached.Value;
        }

        var permissionSet = await LoadPermissionSetAsync(userId, cancellationToken);
        var capabilities = await _capabilityRegistry.GetAllAsync(tenantId, cancellationToken);
        var items = capabilities
            .Where(capability => capability.HostModes.Any(mode =>
                string.Equals(mode, "platform", StringComparison.OrdinalIgnoreCase)))
            .Where(capability => !string.IsNullOrWhiteSpace(capability.PlatformRoute))
            .Where(capability => HasPermission(capability.RequiredPermissions, permissionSet, isPlatformAdmin))
            .Select(capability => new NavigationProjectionItem(
                Key: capability.CapabilityKey,
                Title: capability.Title,
                Path: NormalizeRoute(capability.PlatformRoute, null, null),
                PermissionCode: capability.RequiredPermissions.FirstOrDefault(),
                Order: capability.Navigation.Order ?? int.MaxValue,
                SourceRefs: ["capability-manifest"]))
            .ToArray();

        var groups = BuildGroups(items, capabilities.ToDictionary(
            capability => capability.CapabilityKey,
            capability => capability.Navigation.Group ?? "general",
            StringComparer.OrdinalIgnoreCase));

        var response = new NavigationProjectionResponse(
            HostMode: "platform",
            Scope: new NavigationProjectionScope(tenantId.Value.ToString(), null, null),
            Groups: groups,
            GeneratedAt: DateTimeOffset.UtcNow.ToString("O"));
        await _cache.SetAsync(
            cacheKey,
            response,
            TimeSpan.FromMinutes(3),
            [$"nav-projection:{tenantId.Value:D}", $"nav-projection-user:{tenantId.Value:D}:{userId}"],
            cancellationToken: cancellationToken);
        return response;
    }

    public async Task<NavigationProjectionResponse> GetWorkspaceProjectionAsync(
        TenantId tenantId,
        long appInstanceId,
        long userId,
        bool isPlatformAdmin,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"nav:workspace:{tenantId.Value:D}:{appInstanceId}:{userId}:{isPlatformAdmin}";
        var cached = await _cache.TryGetAsync<NavigationProjectionResponse>(cacheKey, cancellationToken: cancellationToken);
        if (cached.Found && cached.Value is not null)
        {
            return cached.Value;
        }

        var app = await _db.Queryable<LowCodeApp>()
            .FirstAsync(item => item.Id == appInstanceId, cancellationToken);
        var appKey = app?.AppKey;

        var permissionSet = await LoadPermissionSetAsync(userId, cancellationToken);
        var capabilities = await _capabilityRegistry.GetAllAsync(tenantId, cancellationToken);
        var items = capabilities
            .Where(capability => capability.HostModes.Any(mode =>
                string.Equals(mode, "app", StringComparison.OrdinalIgnoreCase)))
            .Where(capability => !string.IsNullOrWhiteSpace(capability.AppRoute))
            .Where(capability => HasPermission(capability.RequiredPermissions, permissionSet, isPlatformAdmin))
            .Select(capability => new NavigationProjectionItem(
                Key: capability.CapabilityKey,
                Title: capability.Title,
                Path: NormalizeRoute(capability.AppRoute, appInstanceId.ToString(), appKey),
                PermissionCode: capability.RequiredPermissions.FirstOrDefault(),
                Order: capability.Navigation.Order ?? int.MaxValue,
                SourceRefs: ["capability-manifest"]))
            .ToArray();
        var deduplicatedItems = DeduplicateWorkspaceMenuItems(items);

        var groups = BuildGroups(deduplicatedItems, capabilities.ToDictionary(
            capability => capability.CapabilityKey,
            capability => capability.Navigation.Group ?? "general",
            StringComparer.OrdinalIgnoreCase));

        var response = new NavigationProjectionResponse(
            HostMode: "app",
            Scope: new NavigationProjectionScope(tenantId.Value.ToString(), appInstanceId.ToString(), appKey),
            Groups: groups,
            GeneratedAt: DateTimeOffset.UtcNow.ToString("O"));
        await _cache.SetAsync(
            cacheKey,
            response,
            TimeSpan.FromMinutes(3),
            [$"nav-projection:{tenantId.Value:D}", $"nav-projection-app:{tenantId.Value:D}:{appInstanceId}", $"nav-projection-user:{tenantId.Value:D}:{userId}"],
            cancellationToken: cancellationToken);
        return response;
    }

    public async Task<NavigationProjectionResponse> GetWorkspaceProjectionByAppKeyAsync(
        TenantId tenantId,
        string appKey,
        long userId,
        bool isPlatformAdmin,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appKey))
        {
            return new NavigationProjectionResponse(
                HostMode: "app",
                Scope: new NavigationProjectionScope(tenantId.Value.ToString(), null, null),
                Groups: [],
                GeneratedAt: DateTimeOffset.UtcNow.ToString("O"));
        }

        var normalizedAppKey = appKey.Trim();
        var app = await _db.Queryable<LowCodeApp>()
            .FirstAsync(item => item.AppKey == normalizedAppKey, cancellationToken);
        if (app is null)
        {
            return new NavigationProjectionResponse(
                HostMode: "app",
                Scope: new NavigationProjectionScope(tenantId.Value.ToString(), null, normalizedAppKey),
                Groups: [],
                GeneratedAt: DateTimeOffset.UtcNow.ToString("O"));
        }

        return await GetWorkspaceProjectionAsync(
            tenantId,
            app.Id,
            userId,
            isPlatformAdmin,
            cancellationToken);
    }

    public async Task<NavigationProjectionResponse> GetRuntimeProjectionAsync(
        TenantId tenantId,
        long userId,
        bool isPlatformAdmin,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"nav:runtime:{tenantId.Value:D}:{userId}:{isPlatformAdmin}";
        var cached = await _cache.TryGetAsync<NavigationProjectionResponse>(cacheKey, cancellationToken: cancellationToken);
        if (cached.Found && cached.Value is not null)
        {
            return cached.Value;
        }

        var permissionSet = await LoadPermissionSetAsync(userId, cancellationToken);
        NavigationProjectionResponse response;
        if (!HasPermission(["apps:view"], permissionSet, isPlatformAdmin))
        {
            response = new NavigationProjectionResponse(
                HostMode: "runtime",
                Scope: new NavigationProjectionScope(tenantId.Value.ToString(), null, null),
                Groups: [],
                GeneratedAt: DateTimeOffset.UtcNow.ToString("O"));
            await _cache.SetAsync(
                cacheKey,
                response,
                TimeSpan.FromMinutes(2),
                [$"nav-projection:{tenantId.Value:D}", $"nav-projection-user:{tenantId.Value:D}:{userId}"],
                cancellationToken: cancellationToken);
            return response;
        }

        var routes = await _db.Queryable<RuntimeRoute>()
            .Where(item => item.IsActive)
            .OrderBy(item => item.AppKey)
            .OrderBy(item => item.PageKey)
            .ToListAsync(cancellationToken);

        var groups = routes
            .GroupBy(route => route.AppKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => new NavigationProjectionGroup(
                GroupKey: $"runtime-{group.Key}",
                GroupTitle: group.Key,
                Items: group
                    .Select((route, index) => new NavigationProjectionItem(
                        Key: $"{route.AppKey}:{route.PageKey}",
                        Title: route.PageKey,
                        Path: $"/r/{Uri.EscapeDataString(route.AppKey)}/{Uri.EscapeDataString(route.PageKey)}",
                        PermissionCode: "apps:view",
                        Order: index + 1,
                        SourceRefs: ["runtime-route"]))
                    .ToArray()))
            .OrderBy(group => group.GroupTitle, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        response = new NavigationProjectionResponse(
            HostMode: "runtime",
            Scope: new NavigationProjectionScope(tenantId.Value.ToString(), null, null),
            Groups: groups,
            GeneratedAt: DateTimeOffset.UtcNow.ToString("O"));
        await _cache.SetAsync(
            cacheKey,
            response,
            TimeSpan.FromMinutes(2),
            [$"nav-projection:{tenantId.Value:D}", $"nav-projection-user:{tenantId.Value:D}:{userId}"],
            cancellationToken: cancellationToken);
        return response;
    }

    private static IReadOnlyList<NavigationProjectionGroup> BuildGroups(
        IReadOnlyList<NavigationProjectionItem> items,
        IReadOnlyDictionary<string, string> itemGroupMap)
    {
        return items
            .GroupBy(item =>
            {
                if (itemGroupMap.TryGetValue(item.Key, out var group))
                {
                    return group;
                }

                return "general";
            }, StringComparer.OrdinalIgnoreCase)
            .Select(group => new NavigationProjectionGroup(
                GroupKey: group.Key,
                GroupTitle: group.Key,
                Items: group
                    .OrderBy(item => item.Order)
                    .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .OrderBy(group => group.GroupTitle, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<NavigationProjectionItem> DeduplicateWorkspaceMenuItems(
        IReadOnlyList<NavigationProjectionItem> items)
    {
        return items
            .Select(item => new
            {
                Item = item,
                NormalizedPath = NormalizeMenuPath(item.Path)
            })
            .GroupBy(entry => (entry.NormalizedPath), StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(entry => entry.Item.Order)
                .ThenBy(entry => entry.Item.Title, StringComparer.OrdinalIgnoreCase)
                .Select(entry => entry.Item)
                .First())
            .ToArray();
    }

    private static string NormalizeMenuPath(string? route)
    {
        return string.IsNullOrWhiteSpace(route)
            ? string.Empty
            : route.Trim().TrimEnd('/').ToLowerInvariant();
    }

    private async Task<HashSet<string>> LoadPermissionSetAsync(long userId, CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var roleIds = await _db.Queryable<UserRole>()
            .Where(item => item.UserId == userId)
            .Select(item => item.RoleId)
            .ToListAsync(cancellationToken);
        if (roleIds.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var roleIdArray = roleIds.Distinct().ToArray();
        var permissionIds = await _db.Queryable<RolePermission>()
            .Where(item => SqlFunc.ContainsArray(roleIdArray, item.RoleId))
            .Select(item => item.PermissionId)
            .ToListAsync(cancellationToken);
        if (permissionIds.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var permissionIdArray = permissionIds.Distinct().ToArray();
        var permissionCodes = await _db.Queryable<Permission>()
            .Where(item => SqlFunc.ContainsArray(permissionIdArray, item.Id))
            .Select(item => item.Code)
            .ToListAsync(cancellationToken);

        return permissionCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasPermission(
        IReadOnlyList<string> requiredPermissions,
        IReadOnlySet<string> permissionSet,
        bool isPlatformAdmin)
    {
        if (isPlatformAdmin)
        {
            return true;
        }

        if (permissionSet.Contains("*:*:*"))
        {
            return true;
        }

        if (permissionSet.Contains(PermissionCodes.SystemAdmin))
        {
            return true;
        }

        if (requiredPermissions.Count == 0)
        {
            return true;
        }

        return requiredPermissions.Any(permissionSet.Contains);
    }

    private static string NormalizeRoute(string? routeTemplate, string? appInstanceId, string? appKey)
    {
        if (string.IsNullOrWhiteSpace(routeTemplate))
        {
            return "/";
        }

        if (routeTemplate.Contains("{app", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(appInstanceId)
            && string.IsNullOrWhiteSpace(appKey))
        {
            return "/console/catalog";
        }

        return routeTemplate
            .Replace("{appInstanceId}", appInstanceId ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{appId}", appInstanceId ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{appKey}", appKey ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
