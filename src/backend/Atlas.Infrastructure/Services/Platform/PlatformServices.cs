using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
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

    public Task<PlatformResourcesResponse> GetResourcesAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var items = new List<PlatformResourceItem>
        {
            new("database", "healthy", "state", "ok"),
            new("runtime-routes", "active", "state", "ok")
        };
        return Task.FromResult(new PlatformResourcesResponse(items));
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
        var formCount = await _db.Queryable<Atlas.Domain.LowCode.Entities.FormDefinition>().CountAsync(cancellationToken);
        var flowCount = await _db.Queryable<Atlas.Domain.Approval.Entities.ApprovalFlowDefinition>().CountAsync(cancellationToken);
        var tableCount = await _db.Queryable<Atlas.Domain.DynamicTables.Entities.DynamicTable>().CountAsync(cancellationToken);
        return new WorkspaceOverviewResponse(pageCount, formCount, flowCount, tableCount);
    }

    public Task<PagedResult<object>> GetWorkspacePagesAsync(TenantId tenantId, long id, PagedRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new PagedResult<object>(Array.Empty<object>(), 0, request.PageIndex <= 0 ? 1 : request.PageIndex, request.PageSize <= 0 ? 10 : request.PageSize));

    public Task<PagedResult<object>> GetWorkspaceFormsAsync(TenantId tenantId, long id, PagedRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new PagedResult<object>(Array.Empty<object>(), 0, request.PageIndex <= 0 ? 1 : request.PageIndex, request.PageSize <= 0 ? 10 : request.PageSize));

    public Task<PagedResult<object>> GetWorkspaceFlowsAsync(TenantId tenantId, long id, PagedRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new PagedResult<object>(Array.Empty<object>(), 0, request.PageIndex <= 0 ? 1 : request.PageIndex, request.PageSize <= 0 ? 10 : request.PageSize));

    public Task<PagedResult<object>> GetWorkspaceDataAsync(TenantId tenantId, long id, PagedRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new PagedResult<object>(Array.Empty<object>(), 0, request.PageIndex <= 0 ? 1 : request.PageIndex, request.PageSize <= 0 ? 10 : request.PageSize));

    public Task<WorkspacePermissionResponse> GetWorkspacePermissionsAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default)
        => Task.FromResult(new WorkspacePermissionResponse(new[] { new WorkspacePermissionItem("apps:view", "应用查看"), new WorkspacePermissionItem("apps:update", "应用更新") }));

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

    public RuntimeRouteQueryService(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<RuntimePageResponse?> GetRuntimePageAsync(TenantId tenantId, string appKey, string pageKey, CancellationToken cancellationToken = default)
    {
        var route = await _db.Queryable<RuntimeRoute>()
            .FirstAsync(x => x.AppKey == appKey && x.PageKey == pageKey && x.IsActive, cancellationToken);
        return route is null
            ? null
            : new RuntimePageResponse(route.AppKey, route.PageKey, route.SchemaVersion, route.IsActive);
    }

    public Task<PagedResult<RuntimeTaskListItem>> GetRuntimeTasksAsync(TenantId tenantId, long userId, PagedRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new PagedResult<RuntimeTaskListItem>(Array.Empty<RuntimeTaskListItem>(), 0, request.PageIndex <= 0 ? 1 : request.PageIndex, request.PageSize <= 0 ? 10 : request.PageSize));

    public Task<bool> ExecuteRuntimeTaskActionAsync(TenantId tenantId, long userId, long taskId, RuntimeTaskActionRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}
