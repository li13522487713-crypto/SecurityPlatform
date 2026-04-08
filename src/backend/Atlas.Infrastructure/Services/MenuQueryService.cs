using AutoMapper;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Caching;

namespace Atlas.Infrastructure.Services;

public sealed class MenuQueryService : IMenuQueryService
{
    private static readonly IReadOnlyDictionary<string, string> BuiltInTitleKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["/console"] = "route.console",
        ["/console/apps"] = "route.consoleApps",
        ["/console/resources"] = "route.consoleResources",
        ["/console/releases"] = "route.consoleReleases",
        ["/console/debug"] = "route.consoleDebugLayer",
        ["/console/tools"] = "route.consoleTools",
        ["/console/datasources"] = "route.consoleDatasources",
        ["/console/settings/system/configs"] = "route.consoleSystemConfigs",
        ["/system/notifications"] = "route.notifications",
        ["/system/notifications/manage"] = "route.notificationsManage",
        ["/settings/system/dict-types"] = "route.dictTypes",
        ["/settings/system/datasources"] = "route.datasources",
        ["/settings/system/configs"] = "route.systemConfigs",
        ["/settings/ai/model-configs"] = "route.modelConfigs",
        ["/settings/auth/roles"] = "route.roles",
        ["/settings/system/plugins"] = "route.plugins",
        ["/settings/system/webhooks"] = "route.webhooks",
        ["/monitor/message-queue"] = "route.messageQueue",
        ["/monitor/server-info"] = "route.serverInfo",
        ["/monitor/scheduled-jobs"] = "route.scheduledJobs",
        ["/monitor/writeback-failures"] = "route.writebackMonitor",
        ["/system/login-logs"] = "route.loginLogs",
        ["/system/online-users"] = "route.onlineUsers",
        ["/settings/license"] = "route.license",
        ["/lowcode/apps"] = "route.lowcodeApps",
        ["/lowcode/forms"] = "route.forms",
        ["/lowcode/templates"] = "route.templateMarket",
        ["/workflow"] = "route.workflowList",
        ["/approval/designer"] = "route.approvalDesigner",
        ["/approval/flows/manage"] = "route.approvalFlowManage",
        ["/approval/flows"] = "route.approvalFlows",
        ["/approval/workspace"] = "route.approvalWorkspace",
        ["/settings/org/tenants"] = "route.tenants",
        ["/settings/org/departments"] = "route.departments",
        ["/settings/org/positions"] = "route.positions",
        ["/settings/org/users"] = "route.users",
        ["/settings/auth/menus"] = "route.menus",
        ["/settings/projects"] = "route.projects",
        ["/assets"] = "route.assets",
        ["/audit"] = "route.audit",
        ["/alert"] = "route.alert"
    };

    private readonly IMenuRepository _menuRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRoleMenuRepository _roleMenuRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IMapper _mapper;
    private readonly IAtlasHybridCache _cache;

    private static readonly TimeSpan MenuCacheTtl = TimeSpan.FromSeconds(60);

    public MenuQueryService(
        IMenuRepository menuRepository,
        IUserRoleRepository userRoleRepository,
        IRoleMenuRepository roleMenuRepository,
        IRoleRepository roleRepository,
        IMapper mapper,
        IAtlasHybridCache cache)
    {
        _menuRepository = menuRepository;
        _userRoleRepository = userRoleRepository;
        _roleMenuRepository = roleMenuRepository;
        _roleRepository = roleRepository;
        _mapper = mapper;
        _cache = cache;
    }

    public async Task<PagedResult<MenuListItem>> QueryMenusAsync(
        MenuQueryRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var (items, total) = await _menuRepository.QueryPageAsync(
            tenantId,
            pageIndex,
            pageSize,
            request.Keyword,
            request.IsHidden,
            cancellationToken);

        var resultItems = items.Select(x => _mapper.Map<MenuListItem>(x)).ToArray();
        return new PagedResult<MenuListItem>(resultItems, total, pageIndex, pageSize);
    }

    public async Task<IReadOnlyList<MenuListItem>> QueryAllAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var items = await _menuRepository.QueryAllAsync(tenantId, cancellationToken);
        return items.Select(x => _mapper.Map<MenuListItem>(x)).ToArray();
    }

    public async Task<IReadOnlyList<MenuListItem>> SelectMenuTreeByUserIdAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        var cacheKey = AtlasCacheKeys.Identity.MenuTree(tenantId, userId);
        var cached = await _cache.TryGetAsync<MenuListItem[]>(cacheKey, cancellationToken: cancellationToken);
        if (cached.Found && cached.Value is not null)
        {
            return cached.Value;
        }

        var allMenusTask = QueryAllAsync(tenantId, cancellationToken);
        var userRolesTask = _userRoleRepository.QueryByUserIdAsync(tenantId, userId, cancellationToken);
        await Task.WhenAll(allMenusTask, userRolesTask);

        var allMenus = allMenusTask.Result;
        var menuCandidates = allMenus
            .Where(x => string.Equals(x.Status, "0", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.SortOrder)
            .ToArray();

        var userRoles = userRolesTask.Result;
        if (userRoles.Count == 0)
        {
            return Array.Empty<MenuListItem>();
        }

        var roleIds = userRoles.Select(x => x.RoleId).Distinct().ToArray();
        var roles = await _roleRepository.QueryByIdsAsync(tenantId, roleIds, cancellationToken);
        var isAdmin = roles.Any(r => string.Equals(r.Code, "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(r.Code, "SuperAdmin", StringComparison.OrdinalIgnoreCase));

        IReadOnlyList<MenuListItem> result;

        if (isAdmin)
        {
            result = menuCandidates.Where(x => !string.Equals(x.MenuType, "F", StringComparison.OrdinalIgnoreCase)).ToArray();
        }
        else
        {
            // 一次批量查询取代 N 次 per-role 查询
            var allRoleMenus = await _roleMenuRepository.QueryByRoleIdsAsync(tenantId, roleIds, cancellationToken);
            var menuIdSet = allRoleMenus.Select(x => x.MenuId).ToHashSet();

            if (menuIdSet.Count == 0)
            {
                return Array.Empty<MenuListItem>();
            }

            var filtered = menuCandidates
                .Where(x => menuIdSet.Contains(long.Parse(x.Id)) && !string.Equals(x.MenuType, "F", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // 递归补全父目录，确保侧边菜单结构完整
            var byId = menuCandidates.ToDictionary(x => x.Id);
            var visited = filtered.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
            var queue = new Queue<MenuListItem>(filtered);
            while (queue.Count > 0)
            {
                var item = queue.Dequeue();
                if (item.ParentId is null or 0)
                {
                    continue;
                }

                var parentId = item.ParentId.Value.ToString();
                if (!visited.Contains(parentId) && byId.TryGetValue(parentId, out var parent))
                {
                    visited.Add(parentId);
                    filtered.Add(parent);
                    queue.Enqueue(parent);
                }
            }

            result = filtered.OrderBy(x => x.SortOrder).ToArray();
        }

        await _cache.SetAsync(
            cacheKey,
            result.ToArray(),
            MenuCacheTtl,
            [AtlasCacheTags.IdentityUser(tenantId, userId), AtlasCacheTags.IdentityTenant(tenantId)],
            cancellationToken: cancellationToken);
        return result;
    }

    public IReadOnlyList<RouterVo> BuildMenus(IReadOnlyList<MenuListItem> menus)
    {
        var nodeMap = menus.ToDictionary(
            x => x.Id,
            x => new RouterVo
            {
                Hidden = x.Visible == "1" || x.IsHidden,
                Name = BuildRouteName(x),
                Path = x.Path,
                Query = x.Query,
                Component = string.IsNullOrWhiteSpace(x.Component) ? null : x.Component,
                Meta = new RouterMeta
                {
                    Title = x.Name,
                    TitleKey = ResolveTitleKey(x.Path),
                    Icon = x.Icon,
                    NoCache = !x.IsCache,
                    Link = x.MenuType == "L" ? x.Path : null,
                    Permi = string.IsNullOrWhiteSpace(x.Perms) ? x.PermissionCode : x.Perms
                },
                Children = new List<RouterVo>()
            },
            StringComparer.Ordinal);

        var roots = new List<RouterVo>();
        foreach (var menu in menus.OrderBy(x => x.SortOrder))
        {
            if (!nodeMap.TryGetValue(menu.Id, out var node))
            {
                continue;
            }

            var isRoot = menu.ParentId is null or 0;
            if (isRoot)
            {
                roots.Add(node);
                continue;
            }

            var parentId = menu.ParentId?.ToString();
            if (!string.IsNullOrWhiteSpace(parentId) && nodeMap.TryGetValue(parentId, out var parent))
            {
                parent.Children ??= new List<RouterVo>();
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        foreach (var root in roots)
        {
            NormalizeRouter(root);
        }

        return roots;
    }

    private static string BuildRouteName(MenuListItem item)
    {
        var path = item.Path.Trim('/');
        if (string.IsNullOrWhiteSpace(path))
        {
            return "Home";
        }

        var parts = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => string.Concat(part
                .Split('-', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => char.ToUpperInvariant(s[0]) + s[1..])))
            .ToArray();
        return string.Join(string.Empty, parts);
    }

    private static void NormalizeRouter(RouterVo router)
    {
        if (router.Children is { Count: > 0 })
        {
            router.AlwaysShow = true;
            foreach (var child in router.Children)
            {
                NormalizeRouter(child);
            }
        }
        else
        {
            router.Children = null;
        }
    }

    private static string? ResolveTitleKey(string path)
    {
        return BuiltInTitleKeys.TryGetValue(path, out var titleKey) ? titleKey : null;
    }
}
