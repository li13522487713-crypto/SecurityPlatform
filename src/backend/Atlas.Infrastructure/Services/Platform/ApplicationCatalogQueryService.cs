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


public sealed class ApplicationCatalogQueryService : IApplicationCatalogQueryService
{
    private readonly ISqlSugarClient _db;
    private readonly IAppManifestQueryService _appManifestQueryService;

    public ApplicationCatalogQueryService(
        ISqlSugarClient db,
        IAppManifestQueryService appManifestQueryService)
    {
        _db = db;
        _appManifestQueryService = appManifestQueryService;
    }

    public async Task<PagedResult<ApplicationCatalogListItem>> QueryAsync(
        TenantId tenantId,
        PagedRequest request,
        string? status = null,
        string? category = null,
        string? appKey = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _appManifestQueryService.QueryAsync(
            tenantId,
            request,
            status,
            category,
            appKey,
            cancellationToken);
        var catalogIds = result.Items
            .Select(item => long.TryParse(item.Id, out var parsedId) ? parsedId : 0)
            .Where(idValue => idValue > 0)
            .Distinct()
            .ToArray();
        var boundCatalogIds = catalogIds.Length == 0
            ? new HashSet<long>()
            : (await _db.Queryable<TenantApplication>()
                .Where(item => item.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(catalogIds, item.CatalogId))
                .Select(item => item.CatalogId)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet();
        var items = result.Items
            .Select(item => new ApplicationCatalogListItem(
                item.Id,
                item.AppKey,
                item.Name,
                item.Status,
                item.Version,
                item.Description,
                item.Category,
                item.Icon,
                item.PublishedAt,
                long.TryParse(item.Id, out var catalogId) && boundCatalogIds.Contains(catalogId)))
            .ToArray();

        return new PagedResult<ApplicationCatalogListItem>(items, result.Total, result.PageIndex, result.PageSize);
    }

    public async Task<ApplicationCatalogDetail?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var item = await _appManifestQueryService.GetByIdAsync(tenantId, id, cancellationToken);
        if (item is null)
        {
            return null;
        }
        var isBound = await _db.Queryable<TenantApplication>()
            .AnyAsync(row => row.TenantIdValue == tenantId.Value && row.CatalogId == id, cancellationToken);
        var dataSourceId = await _db.Queryable<AppManifest>()
            .Where(row => row.TenantIdValue == tenantId.Value && row.Id == id)
            .Select(row => row.DataSourceId)
            .FirstAsync(cancellationToken);

        return new ApplicationCatalogDetail(
            item.Id,
            item.AppKey,
            item.Name,
            item.Status,
            item.Version,
            item.Description,
            item.Category,
            item.Icon,
            item.PublishedAt,
            dataSourceId?.ToString(),
            isBound);
    }
}

