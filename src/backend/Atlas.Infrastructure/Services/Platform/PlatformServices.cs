using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Application.Approval.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using Atlas.Domain.Identity.Entities;
using Atlas.Domain.LowCode.Entities;
using Atlas.Domain.Platform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.Platform;

public sealed class PlatformQueryService : IPlatformQueryService
{
    private readonly ISqlSugarClient _db;

    public PlatformQueryService(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<PlatformOverviewResponse> GetOverviewAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var appCount = await _db.Queryable<AppManifest>().CountAsync(cancellationToken);
        var releaseCount = await _db.Queryable<AppRelease>().CountAsync(cancellationToken);
        var activeRouteCount = await _db.Queryable<RuntimeRoute>().CountAsync(x => x.IsActive, cancellationToken);
        var policyCount = await _db.Queryable<ToolAuthorizationPolicy>().CountAsync(cancellationToken);
        var licenseCount = await _db.Queryable<LicenseGrant>().CountAsync(cancellationToken);
        return new PlatformOverviewResponse(appCount, releaseCount, activeRouteCount, policyCount, licenseCount);
    }

    public async Task<PlatformResourcesResponse> GetResourcesAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var dbConnectionString = _db.CurrentConnectionConfig.ConnectionString;
        var dbFile = ParseSqliteDataSource(dbConnectionString);
        long dbSize = 0;
        if (!string.IsNullOrWhiteSpace(dbFile) && File.Exists(dbFile))
        {
            dbSize = new FileInfo(dbFile).Length;
        }

        var activeSessionCount = await _db.Queryable<Atlas.Domain.Identity.Entities.AuthSession>()
            .CountAsync(x => x.RevokedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow, cancellationToken);
        var apiCallCount = await _db.Queryable<Atlas.Domain.Audit.Entities.AuditRecord>()
            .CountAsync(cancellationToken);
        var routeCount = await _db.Queryable<RuntimeRoute>().CountAsync(x => x.IsActive, cancellationToken);

        var items = new List<PlatformResourceItem>
        {
            new("database-size", dbSize.ToString(), "bytes", "ok"),
            new("active-sessions", activeSessionCount.ToString(), "count", "ok"),
            new("audit-api-calls", apiCallCount.ToString(), "count", "ok"),
            new("runtime-routes", routeCount.ToString(), "count", "ok")
        };
        return new PlatformResourcesResponse(items);
    }

    public async Task<PagedResult<AppReleaseResponse>> GetReleasesAsync(TenantId tenantId, PagedRequest request, CancellationToken cancellationToken = default)
    {
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var query = _db.Queryable<AppRelease>().OrderByDescending(x => x.ReleasedAt);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(x => new AppReleaseResponse(
            x.Id.ToString(),
            x.ManifestId.ToString(),
            x.Version,
            x.Status.ToString(),
            x.ReleasedAt.ToString("O"),
            x.ReleaseNote)).ToArray();
        return new PagedResult<AppReleaseResponse>(items, total, pageIndex, pageSize);
    }

    private static string ParseSqliteDataSource(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return string.Empty;
        }

        const string key = "Data Source=";
        var index = connectionString.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return string.Empty;
        }

        var value = connectionString[(index + key.Length)..];
        var semicolon = value.IndexOf(';');
        if (semicolon >= 0)
        {
            value = value[..semicolon];
        }

        return value.Trim();
    }
}

public sealed class AppManifestQueryService : IAppManifestQueryService
{
    private readonly ISqlSugarClient _db;

    public AppManifestQueryService(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<PagedResult<AppManifestResponse>> QueryAsync(TenantId tenantId, PagedRequest request, CancellationToken cancellationToken = default)
    {
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var query = _db.Queryable<AppManifest>();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(x => x.Name.Contains(keyword) || x.AppKey.Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.UpdatedAt).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(MapManifest).ToArray();
        return new PagedResult<AppManifestResponse>(items, total, pageIndex, pageSize);
    }

    public async Task<AppManifestResponse?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Queryable<AppManifest>().FirstAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : MapManifest(entity);
    }

    public async Task<WorkspaceOverviewResponse> GetWorkspaceOverviewAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default)
    {
        var pageCount = await _db.Queryable<Atlas.Domain.LowCode.Entities.LowCodePage>().CountAsync(x => x.AppId == id, cancellationToken);
        var tableCount = await _db.Queryable<Atlas.Domain.DynamicTables.Entities.DynamicTable>()
            .CountAsync(x => x.AppId == id, cancellationToken);
        var tableKeys = await _db.Queryable<Atlas.Domain.DynamicTables.Entities.DynamicTable>()
            .Where(x => x.AppId == id)
            .Select(x => x.TableKey)
            .ToListAsync(cancellationToken);
        var formCount = tableKeys.Count == 0
            ? 0
            : await _db.Queryable<Atlas.Domain.LowCode.Entities.FormDefinition>()
                .CountAsync(x => x.DataTableKey != null && SqlFunc.ContainsArray(tableKeys.ToArray(), x.DataTableKey!), cancellationToken);
        var flowCount = await _db.Queryable<Atlas.Domain.DynamicTables.Entities.DynamicTable>()
            .Where(table => table.AppId == id && table.ApprovalFlowDefinitionId != null)
            .Select(table => table.ApprovalFlowDefinitionId!.Value)
            .Distinct()
            .CountAsync(cancellationToken);
        return new WorkspaceOverviewResponse(pageCount, formCount, flowCount, tableCount);
    }

    public async Task<PagedResult<object>> GetWorkspacePagesAsync(TenantId tenantId, long id, PagedRequest request, CancellationToken cancellationToken = default)
    {
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var query = _db.Queryable<LowCodePage>().Where(x => x.AppId == id);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(x => x.Name.Contains(keyword) || x.PageKey.Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(x => x.SortOrder).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(x => (object)new
        {
            Id = x.Id.ToString(),
            x.PageKey,
            x.Name,
            x.RoutePath,
            x.Icon,
            x.SortOrder,
            x.Version,
            x.IsPublished
        }).ToArray();
        return new PagedResult<object>(items, total, pageIndex, pageSize);
    }

    public async Task<PagedResult<object>> GetWorkspaceFormsAsync(TenantId tenantId, long id, PagedRequest request, CancellationToken cancellationToken = default)
    {
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var tableKeys = await _db.Queryable<Atlas.Domain.DynamicTables.Entities.DynamicTable>()
            .Where(x => x.AppId == id)
            .Select(x => x.TableKey)
            .ToListAsync(cancellationToken);
        if (tableKeys.Count == 0)
        {
            return new PagedResult<object>(Array.Empty<object>(), 0, pageIndex, pageSize);
        }

        var tableKeyArray = tableKeys.Distinct().ToArray();
        var query = _db.Queryable<FormDefinition>()
            .Where(x => x.DataTableKey != null && SqlFunc.ContainsArray(tableKeyArray, x.DataTableKey!));
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(form => form.Name.Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(form => form.UpdatedAt).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(x => (object)new
        {
            Id = x.Id.ToString(),
            x.Name,
            x.Category,
            x.DataTableKey,
            x.Version,
            Status = x.Status.ToString(),
            UpdatedAt = x.UpdatedAt.ToString("O")
        }).ToArray();
        return new PagedResult<object>(items, total, pageIndex, pageSize);
    }

    public async Task<PagedResult<object>> GetWorkspaceFlowsAsync(TenantId tenantId, long id, PagedRequest request, CancellationToken cancellationToken = default)
    {
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var appFlowIds = await _db.Queryable<Atlas.Domain.DynamicTables.Entities.DynamicTable>()
            .Where(x => x.AppId == id && x.ApprovalFlowDefinitionId != null)
            .Select(x => x.ApprovalFlowDefinitionId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (appFlowIds.Count == 0)
        {
            return new PagedResult<object>(Array.Empty<object>(), 0, pageIndex, pageSize);
        }

        var query = _db.Queryable<Atlas.Domain.Approval.Entities.ApprovalFlowDefinition>()
            .Where(x => SqlFunc.ContainsArray(appFlowIds.ToArray(), x.Id));
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(x => x.Name.Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.PublishedAt).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(x => (object)new
        {
            Id = x.Id.ToString(),
            x.Name,
            x.Category,
            x.Version,
            Status = x.Status.ToString(),
            UpdatedAt = x.PublishedAt?.ToString("O") ?? string.Empty
        }).ToArray();
        return new PagedResult<object>(items, total, pageIndex, pageSize);
    }

    public async Task<PagedResult<object>> GetWorkspaceDataAsync(TenantId tenantId, long id, PagedRequest request, CancellationToken cancellationToken = default)
    {
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var query = _db.Queryable<Atlas.Domain.DynamicTables.Entities.DynamicTable>().Where(x => x.AppId == id);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(x => x.TableKey.Contains(keyword) || x.DisplayName.Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.UpdatedAt).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(x => (object)new
        {
            Id = x.Id.ToString(),
            x.TableKey,
            x.DisplayName,
            x.DbType,
            x.Status,
            UpdatedAt = x.UpdatedAt.ToString("O")
        }).ToArray();
        return new PagedResult<object>(items, total, pageIndex, pageSize);
    }

    public async Task<WorkspacePermissionResponse> GetWorkspacePermissionsAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default)
    {
        var permissions = await _db.Queryable<Permission>()
            .Where(x => x.Code.StartsWith("apps:") || x.Code.StartsWith("approval:") || x.Code.StartsWith("dynamic:"))
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);
        var items = permissions.Select(x => new WorkspacePermissionItem(x.Code, x.Name)).ToArray();
        return new WorkspacePermissionResponse(items);
    }

    private static AppManifestResponse MapManifest(AppManifest entity)
        => new(
            entity.Id.ToString(),
            entity.AppKey,
            entity.Name,
            entity.Status.ToString(),
            entity.Version,
            entity.Description,
            entity.Category,
            entity.Icon,
            entity.PublishedAt?.ToString("O"));
}

public sealed class AppManifestCommandService : IAppManifestCommandService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGenerator;

    public AppManifestCommandService(ISqlSugarClient db, IIdGeneratorAccessor idGenerator)
    {
        _db = db;
        _idGenerator = idGenerator;
    }

    public async Task<long> CreateAsync(TenantId tenantId, long userId, AppManifestCreateRequest request, CancellationToken cancellationToken = default)
    {
        var exists = await _db.Queryable<AppManifest>().AnyAsync(x => x.AppKey == request.AppKey, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException($"应用标识 {request.AppKey} 已存在");
        }

        var entity = new AppManifest(tenantId, _idGenerator.NextId(), request.AppKey, request.Name, userId, DateTimeOffset.UtcNow);
        entity.Update(request.Name, request.Description, request.Category, request.Icon, request.DataSourceId, userId, DateTimeOffset.UtcNow);
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(TenantId tenantId, long userId, long id, AppManifestUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Queryable<AppManifest>().FirstAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("应用不存在");
        entity.Update(request.Name, request.Description, request.Category, request.Icon, request.DataSourceId, userId, DateTimeOffset.UtcNow);
        await _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task ArchiveAsync(TenantId tenantId, long userId, long id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Queryable<AppManifest>().FirstAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("应用不存在");
        entity.Archive(userId, DateTimeOffset.UtcNow);
        await _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }
}

public sealed class AppReleaseCommandService : IAppReleaseCommandService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGenerator;

    public AppReleaseCommandService(ISqlSugarClient db, IIdGeneratorAccessor idGenerator)
    {
        _db = db;
        _idGenerator = idGenerator;
    }

    public async Task<long> CreateReleaseAsync(TenantId tenantId, long userId, long manifestId, string? releaseNote, CancellationToken cancellationToken = default)
    {
        var manifest = await _db.Queryable<AppManifest>().FirstAsync(x => x.Id == manifestId, cancellationToken)
            ?? throw new InvalidOperationException("应用不存在");
        var now = DateTimeOffset.UtcNow;
        manifest.Publish(userId, now);
        await _db.Updateable(manifest).ExecuteCommandAsync(cancellationToken);
        var release = new AppRelease(tenantId, _idGenerator.NextId(), manifestId, manifest.Version, "{}", userId, now);
        await _db.Insertable(release).ExecuteCommandAsync(cancellationToken);
        return release.Id;
    }

    public async Task RollbackAsync(TenantId tenantId, long userId, long manifestId, long releaseId, CancellationToken cancellationToken = default)
    {
        var rollbackTarget = await _db.Queryable<AppRelease>()
            .FirstAsync(x => x.Id == releaseId && x.ManifestId == manifestId, cancellationToken)
            ?? throw new InvalidOperationException("回滚目标发布记录不存在");
        var currentRelease = await _db.Queryable<AppRelease>()
            .Where(x => x.ManifestId == manifestId && x.Status == AppReleaseStatus.Released)
            .OrderByDescending(x => x.ReleasedAt)
            .FirstAsync(cancellationToken)
            ?? throw new InvalidOperationException("当前发布版本不存在");
        if (currentRelease.Id == rollbackTarget.Id)
        {
            throw new InvalidOperationException("当前已是目标版本，无需回滚");
        }

        currentRelease.MarkRolledBack(rollbackTarget.Id);
        await _db.Updateable(currentRelease).ExecuteCommandAsync(cancellationToken);
    }
}

public sealed class RuntimeRouteQueryService : IRuntimeRouteQueryService
{
    private readonly ISqlSugarClient _db;
    private readonly IApprovalOperationService _approvalOperationService;

    public RuntimeRouteQueryService(
        ISqlSugarClient db,
        IApprovalOperationService approvalOperationService)
    {
        _db = db;
        _approvalOperationService = approvalOperationService;
    }

    public async Task<RuntimePageResponse?> GetRuntimePageAsync(TenantId tenantId, string appKey, string pageKey, CancellationToken cancellationToken = default)
    {
        var route = await _db.Queryable<RuntimeRoute>()
            .FirstAsync(x => x.AppKey == appKey && x.PageKey == pageKey && x.IsActive, cancellationToken);
        return route is null
            ? null
            : new RuntimePageResponse(route.AppKey, route.PageKey, route.SchemaVersion, route.IsActive);
    }

    public async Task<PagedResult<RuntimeTaskListItem>> GetRuntimeTasksAsync(
        TenantId tenantId,
        long userId,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var assignee = userId.ToString();
        var query = _db.Queryable<ApprovalTask>()
            .Where(x => x.AssigneeValue == assignee && x.Status == ApprovalTaskStatus.Pending);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.CreatedAt).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(x => new RuntimeTaskListItem(
            x.Id.ToString(),
            "approval",
            x.Title,
            x.Status.ToString(),
            x.CreatedAt.ToString("O"))).ToArray();
        return new PagedResult<RuntimeTaskListItem>(items, total, pageIndex, pageSize);
    }

    public async Task<RuntimeMenuResponse> GetRuntimeMenuAsync(TenantId tenantId, string appKey, CancellationToken cancellationToken = default)
    {
        var routes = await _db.Queryable<RuntimeRoute>()
            .Where(x => x.AppKey == appKey && x.IsActive)
            .OrderBy(x => x.PageKey)
            .ToListAsync(cancellationToken);
        if (routes.Count == 0)
        {
            return new RuntimeMenuResponse(appKey, Array.Empty<RuntimeMenuItem>());
        }

        var pageKeys = routes.Select(x => x.PageKey).Distinct().ToArray();
        var pages = await _db.Queryable<LowCodePage>()
            .Where(x => x.AppId > 0 && SqlFunc.ContainsArray(pageKeys, x.PageKey))
            .ToListAsync(cancellationToken);
        var pageMap = pages.ToDictionary(x => x.PageKey, x => x, StringComparer.OrdinalIgnoreCase);
        var items = routes.Select(route =>
        {
            if (pageMap.TryGetValue(route.PageKey, out var page))
            {
                return new RuntimeMenuItem(
                    route.PageKey,
                    page.Name,
                    string.IsNullOrWhiteSpace(page.RoutePath) ? $"/r/{route.AppKey}/{route.PageKey}" : page.RoutePath!,
                    page.Icon,
                    page.SortOrder);
            }

            return new RuntimeMenuItem(
                route.PageKey,
                route.PageKey,
                $"/r/{route.AppKey}/{route.PageKey}",
                null,
                0);
        }).OrderBy(x => x.SortOrder).ThenBy(x => x.Title).ToArray();

        return new RuntimeMenuResponse(appKey, items);
    }

    public async Task<bool> ExecuteRuntimeTaskActionAsync(
        TenantId tenantId,
        long userId,
        long taskId,
        RuntimeTaskActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var task = await _db.Queryable<ApprovalTask>()
            .FirstAsync(x => x.Id == taskId, cancellationToken);
        if (task is null)
        {
            return false;
        }

        var operationType = request.Action.Trim().ToLowerInvariant() switch
        {
            "approve" => ApprovalOperationType.Agree,
            "reject" => ApprovalOperationType.Disagree,
            "transfer" => ApprovalOperationType.Transfer,
            "delegate" => ApprovalOperationType.Delegate,
            "return" => ApprovalOperationType.BackToPrevModify,
            _ => ApprovalOperationType.Agree
        };
        var opRequest = new Atlas.Application.Approval.Models.ApprovalOperationRequest
        {
            OperationType = operationType,
            Comment = request.Comment
        };
        await _approvalOperationService.ExecuteOperationAsync(
            tenantId,
            task.InstanceId,
            task.Id,
            userId,
            opRequest,
            cancellationToken);
        return true;
    }
}
