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


public sealed class TenantApplicationQueryService : ITenantApplicationQueryService
{
    private readonly ISqlSugarClient _db;

    public TenantApplicationQueryService(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<PagedResult<TenantApplicationListItem>> QueryAsync(
        TenantId tenantId,
        PagedRequest request,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;

        var query = _db.Queryable<TenantApplication>()
            .Where(item => item.TenantIdValue == tenantValue);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(item => item.AppKey.Contains(keyword) || item.Name.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TenantApplicationStatus>(status, true, out var statusValue))
        {
            query = query.Where(item => item.Status == statusValue);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(item => item.UpdatedAt)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var catalogIds = rows.Select(item => item.CatalogId).Distinct().ToArray();
        var catalogNameDict = new Dictionary<long, string>();
        if (catalogIds.Length > 0)
        {
            var catalogs = await _db.Queryable<AppManifest>()
                .Where(item => item.TenantIdValue == tenantValue && SqlFunc.ContainsArray(catalogIds, item.Id))
                .Select(item => new { item.Id, item.Name })
                .ToListAsync(cancellationToken);
            catalogNameDict = catalogs.ToDictionary(item => item.Id, item => item.Name);
        }

        var items = rows.Select(item =>
        {
            catalogNameDict.TryGetValue(item.CatalogId, out var catalogName);
            return new TenantApplicationListItem(
                item.Id.ToString(),
                item.CatalogId.ToString(),
                catalogName ?? "Unknown",
                item.AppInstanceId.ToString(),
                item.AppKey,
                item.Name,
                item.Status.ToString(),
                item.OpenedAt.ToString("O"),
                item.DataSourceId?.ToString());
        }).ToArray();

        return new PagedResult<TenantApplicationListItem>(items, total, pageIndex, pageSize);
    }

    public async Task<TenantApplicationDetail?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var item = await _db.Queryable<TenantApplication>()
            .FirstAsync(row => row.TenantIdValue == tenantValue && row.Id == id, cancellationToken);
        if (item is null)
        {
            return null;
        }

        var catalog = await _db.Queryable<AppManifest>()
            .Where(row => row.TenantIdValue == tenantValue && row.Id == item.CatalogId)
            .Select(row => new { row.Name })
            .FirstAsync(cancellationToken);

        return new TenantApplicationDetail(
            item.Id.ToString(),
            item.CatalogId.ToString(),
            catalog?.Name ?? "Unknown",
            item.AppInstanceId.ToString(),
            item.AppKey,
            item.Name,
            item.Status.ToString(),
            item.OpenedAt.ToString("O"),
            item.UpdatedAt.ToString("O"),
            item.DataSourceId?.ToString());
    }
}

