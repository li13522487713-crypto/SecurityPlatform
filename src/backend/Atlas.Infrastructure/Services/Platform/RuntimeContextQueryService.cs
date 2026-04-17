using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Identity;
using Atlas.Application.Options;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.Identity.Entities;
using Atlas.Domain.Platform.Entities;
using Atlas.Domain.System.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SqlSugar;
using System.Text.Json;
using TenantAppDataSourceBindingDto = Atlas.Application.Platform.Models.TenantAppDataSourceBinding;
using TenantAppDataSourceBindingEntity = Atlas.Domain.System.Entities.TenantAppDataSourceBinding;

namespace Atlas.Infrastructure.Services.Platform;


public sealed class RuntimeContextQueryService : IRuntimeContextQueryService
{
    private readonly ISqlSugarClient _mainDb;
    private readonly Atlas.Infrastructure.Services.IAppDbScopeFactory _appDbScopeFactory;

    public RuntimeContextQueryService(
        ISqlSugarClient db,
        Atlas.Infrastructure.Services.IAppDbScopeFactory appDbScopeFactory)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
    }

    public RuntimeContextQueryService(ISqlSugarClient db)
        : this(db, new Atlas.Infrastructure.Services.MainOnlyAppDbScopeFactory(db))
    {
    }

    public async Task<RuntimeContextDetail?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var route = await FindRouteAcrossDbsByIdAsync(tenantId, id, cancellationToken);
        if (route is null)
        {
            return null;
        }

        return new RuntimeContextDetail(
            route.Id.ToString(),
            route.AppKey,
            route.PageKey,
            route.SchemaVersion,
            route.EnvironmentCode,
            route.IsActive);
    }

    public async Task<PagedResult<RuntimeContextListItem>> QueryAsync(
        TenantId tenantId,
        PagedRequest request,
        string? appKey = null,
        string? pageKey = null,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var db = await ResolveRuntimeDbByAppKeyAsync(tenantId, appKey, cancellationToken);
        var query = db.Queryable<RuntimeRoute>()
            .Where(route => route.TenantIdValue == tenantValue);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(route => route.AppKey.Contains(keyword) || route.PageKey.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(appKey))
        {
            var appKeyValue = appKey.Trim();
            query = query.Where(route => route.AppKey == appKeyValue);
        }

        if (!string.IsNullOrWhiteSpace(pageKey))
        {
            var pageKeyValue = pageKey.Trim();
            query = query.Where(route => route.PageKey == pageKeyValue);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(route => route.Id)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows
            .Select(route => new RuntimeContextListItem(
                route.Id.ToString(),
                route.AppKey,
                route.PageKey,
                route.SchemaVersion,
                route.EnvironmentCode,
                route.IsActive))
            .ToArray();

        return new PagedResult<RuntimeContextListItem>(items, total, pageIndex, pageSize);
    }

    public async Task<RuntimeContextDetail?> GetByRouteAsync(
        TenantId tenantId,
        string appKey,
        string pageKey,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var db = await ResolveRuntimeDbByAppKeyAsync(tenantId, appKey, cancellationToken);
        var route = await db.Queryable<RuntimeRoute>()
            .FirstAsync(x => x.TenantIdValue == tenantValue && x.AppKey == appKey && x.PageKey == pageKey, cancellationToken);
        if (route is null)
        {
            return null;
        }

        return new RuntimeContextDetail(
            route.Id.ToString(),
            route.AppKey,
            route.PageKey,
            route.SchemaVersion,
            route.EnvironmentCode,
            route.IsActive);
    }

    private async Task<ISqlSugarClient> ResolveRuntimeDbByAppKeyAsync(
        TenantId tenantId,
        string? appKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appKey))
        {
            return _mainDb;
        }

        _ = appKey;
        return _mainDb;
    }

    private async Task<RuntimeRoute?> FindRouteAcrossDbsByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        var route = await _mainDb.Queryable<RuntimeRoute>()
            .FirstAsync(x => x.TenantIdValue == tenantId.Value && x.Id == id, cancellationToken);
        if (route is not null)
        {
            return route;
        }

        return null;
    }
}

