using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.Identity;
using Atlas.Application.LowCode.Models;
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
using Atlas.Domain.LowCode.Entities;
using Atlas.Domain.LowCode.Enums;
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

public sealed class ApplicationCatalogCommandService : IApplicationCatalogCommandService
{
    private readonly ISqlSugarClient _db;
    private readonly IAppReleaseCommandService _appReleaseCommandService;

    public ApplicationCatalogCommandService(
        ISqlSugarClient db,
        IAppReleaseCommandService appReleaseCommandService)
    {
        _db = db;
        _appReleaseCommandService = appReleaseCommandService;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long userId,
        long id,
        ApplicationCatalogUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "目录名称不能为空。");
        }

        var catalog = await _db.Queryable<AppManifest>()
            .FirstAsync(row => row.TenantIdValue == tenantId.Value && row.Id == id, cancellationToken);
        if (catalog is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "应用目录不存在。");
        }

        catalog.Update(
            name,
            request.Description?.Trim(),
            request.Category?.Trim(),
            request.Icon?.Trim(),
            catalog.DataSourceId,
            userId,
            DateTimeOffset.UtcNow);
        await _db.Updateable(catalog)
            .Where(row => row.Id == catalog.Id && row.TenantIdValue == tenantId.Value)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task PublishAsync(
        TenantId tenantId,
        long userId,
        long id,
        ApplicationCatalogPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        var exists = await _db.Queryable<AppManifest>()
            .AnyAsync(row => row.TenantIdValue == tenantId.Value && row.Id == id, cancellationToken);
        if (!exists)
        {
            throw new BusinessException(ErrorCodes.NotFound, "应用目录不存在。");
        }

        await _appReleaseCommandService.CreateReleaseAsync(
            tenantId,
            userId,
            id,
            request.ReleaseNote,
            cancellationToken);
    }

    public async Task UpdateDataSourceAsync(
        TenantId tenantId,
        long userId,
        long id,
        ApplicationCatalogDataSourceUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(request.DataSourceId, out var dataSourceId) || dataSourceId <= 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "数据源ID必须大于0。");
        }

        var catalog = await _db.Queryable<AppManifest>()
            .FirstAsync(row => row.TenantIdValue == tenantId.Value && row.Id == id, cancellationToken);
        if (catalog is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "应用目录不存在。");
        }

        var hasBinding = await _db.Queryable<TenantApplication>()
            .AnyAsync(row => row.TenantIdValue == tenantId.Value && row.CatalogId == id, cancellationToken);
        if (hasBinding)
        {
            throw new BusinessException(ErrorCodes.Conflict, "应用目录已绑定租户应用，仅未绑定目录允许修改数据源。");
        }

        var tenantIdText = tenantId.Value.ToString("D");
        var dataSourceExists = await _db.Queryable<TenantDataSource>()
            .AnyAsync(
                row => row.TenantIdValue == tenantIdText && row.Id == dataSourceId && row.IsActive,
                cancellationToken);
        if (!dataSourceExists)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "目标数据源不存在或未启用。");
        }

        catalog.Update(
            catalog.Name,
            catalog.Description,
            catalog.Category,
            catalog.Icon,
            dataSourceId,
            userId,
            DateTimeOffset.UtcNow);
        await _db.Updateable(catalog)
            .Where(row => row.Id == catalog.Id && row.TenantIdValue == tenantId.Value)
            .ExecuteCommandAsync(cancellationToken);
    }
}

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
        if (rows.Count == 0)
        {
            return await BuildFallbackResultAsync(tenantId, request, status, cancellationToken);
        }

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
            return await BuildFallbackDetailAsync(tenantId, id, cancellationToken);
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

    private async Task<PagedResult<TenantApplicationListItem>> BuildFallbackResultAsync(
        TenantId tenantId,
        PagedRequest request,
        string? status,
        CancellationToken cancellationToken)
    {
        var tenantValue = tenantId.Value;
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;

        var appQuery = _db.Queryable<LowCodeApp>()
            .Where(item => item.TenantIdValue == tenantValue);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            appQuery = appQuery.Where(item => item.AppKey.Contains(keyword) || item.Name.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TenantApplicationStatus>(status, true, out var statusValue))
        {
            if (statusValue == TenantApplicationStatus.Provisioning)
            {
                return new PagedResult<TenantApplicationListItem>(Array.Empty<TenantApplicationListItem>(), 0, pageIndex, pageSize);
            }

            var appStatus = statusValue switch
            {
                TenantApplicationStatus.Disabled => LowCodeAppStatus.Disabled,
                TenantApplicationStatus.Archived => LowCodeAppStatus.Archived,
                _ => LowCodeAppStatus.Published
            };
            appQuery = appQuery.Where(item => item.Status == appStatus);
        }

        var total = await appQuery.CountAsync(cancellationToken);
        var apps = await appQuery
            .OrderByDescending(item => item.UpdatedAt)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        if (apps.Count == 0)
        {
            return new PagedResult<TenantApplicationListItem>([], 0, pageIndex, pageSize);
        }

        var appKeys = apps.Select(item => item.AppKey).Distinct().ToArray();
        var catalogs = await _db.Queryable<AppManifest>()
            .Where(item => item.TenantIdValue == tenantValue && SqlFunc.ContainsArray(appKeys, item.AppKey))
            .Select(item => new { item.Id, item.AppKey, item.Name })
            .ToListAsync(cancellationToken);
        var catalogByAppKey = catalogs
            .GroupBy(item => item.AppKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.Id).First(), StringComparer.OrdinalIgnoreCase);

        var items = apps.Select(app =>
        {
            catalogByAppKey.TryGetValue(app.AppKey, out var catalog);
            return new TenantApplicationListItem(
                app.Id.ToString(),
                catalog?.Id.ToString() ?? string.Empty,
                catalog?.Name ?? "Unknown",
                app.Id.ToString(),
                app.AppKey,
                app.Name,
                MapTenantApplicationStatus(app.Status).ToString(),
                app.CreatedAt.ToString("O"),
                app.DataSourceId?.ToString());
        }).ToArray();

        return new PagedResult<TenantApplicationListItem>(items, total, pageIndex, pageSize);
    }

    private async Task<TenantApplicationDetail?> BuildFallbackDetailAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        var tenantValue = tenantId.Value;
        var app = await _db.Queryable<LowCodeApp>()
            .FirstAsync(item => item.TenantIdValue == tenantValue && item.Id == id, cancellationToken);
        if (app is null)
        {
            return null;
        }

        var catalog = await _db.Queryable<AppManifest>()
            .Where(item => item.TenantIdValue == tenantValue && item.AppKey == app.AppKey)
            .OrderByDescending(item => item.Id)
            .Select(item => new { item.Id, item.Name })
            .FirstAsync(cancellationToken);

        return new TenantApplicationDetail(
            app.Id.ToString(),
            catalog?.Id.ToString() ?? string.Empty,
            catalog?.Name ?? "Unknown",
            app.Id.ToString(),
            app.AppKey,
            app.Name,
            MapTenantApplicationStatus(app.Status).ToString(),
            app.CreatedAt.ToString("O"),
            app.UpdatedAt.ToString("O"),
            app.DataSourceId?.ToString());
    }

    private static TenantApplicationStatus MapTenantApplicationStatus(LowCodeAppStatus status)
    {
        return status switch
        {
            LowCodeAppStatus.Disabled => TenantApplicationStatus.Disabled,
            LowCodeAppStatus.Archived => TenantApplicationStatus.Archived,
            _ => TenantApplicationStatus.Active
        };
    }
}

public sealed class TenantAppInstanceQueryService : ITenantAppInstanceQueryService
{
    private readonly ILowCodeAppQueryService _lowCodeAppQueryService;
    private readonly ITenantDataSourceService _tenantDataSourceService;
    private readonly ISystemConfigQueryService _systemConfigQueryService;
    private readonly IOptionsMonitor<FileStorageOptions> _fileStorageOptionsMonitor;
    private readonly ISqlSugarClient _db;

    public TenantAppInstanceQueryService(
        ILowCodeAppQueryService lowCodeAppQueryService,
        ITenantDataSourceService tenantDataSourceService,
        ISystemConfigQueryService systemConfigQueryService,
        IOptionsMonitor<FileStorageOptions> fileStorageOptionsMonitor,
        ISqlSugarClient db)
    {
        _lowCodeAppQueryService = lowCodeAppQueryService;
        _tenantDataSourceService = tenantDataSourceService;
        _systemConfigQueryService = systemConfigQueryService;
        _fileStorageOptionsMonitor = fileStorageOptionsMonitor;
        _db = db;
    }

    public async Task<PagedResult<TenantAppInstanceListItem>> QueryAsync(
        TenantId tenantId,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _lowCodeAppQueryService.QueryAsync(request, tenantId, null, cancellationToken);
        var items = result.Items
            .Select(item => new TenantAppInstanceListItem(
                item.Id,
                item.AppKey,
                item.Name,
                item.Status,
                item.Version,
                item.Description,
                item.Category,
                item.Icon,
                item.PublishedAt?.ToString("O")))
            .ToArray();

        return new PagedResult<TenantAppInstanceListItem>(items, result.Total, result.PageIndex, result.PageSize);
    }

    public async Task<TenantAppInstanceDetail?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var item = await _lowCodeAppQueryService.GetByIdAsync(tenantId, id, cancellationToken);
        if (item is null)
        {
            return null;
        }

        return new TenantAppInstanceDetail(
            item.Id,
            item.AppKey,
            item.Name,
            item.Status,
            item.Version,
            item.Description,
            item.Category,
            item.Icon,
            item.PublishedAt?.ToString("O"),
            item.DataSourceId);
    }

    public Task<IReadOnlyList<LowCodeAppEntityAliasItem>> GetEntityAliasesAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        return _lowCodeAppQueryService.GetEntityAliasesAsync(tenantId, id, cancellationToken);
    }

    public Task<LowCodeAppDataSourceInfo?> GetDataSourceInfoAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        return _lowCodeAppQueryService.GetDataSourceInfoAsync(tenantId, id, cancellationToken);
    }

    public async Task<TestConnectionResult> TestDataSourceAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var dataSourceInfo = await _lowCodeAppQueryService.GetDataSourceInfoAsync(tenantId, id, cancellationToken);
        if (dataSourceInfo is null)
        {
            return new TestConnectionResult(false, "应用不存在");
        }

        if (!long.TryParse(dataSourceInfo.DataSourceId, out var dataSourceId))
        {
            return new TestConnectionResult(false, "应用未绑定数据源");
        }

        return await _tenantDataSourceService.TestConnectionByDataSourceIdAsync(
            tenantId.Value.ToString(),
            dataSourceId,
            cancellationToken);
    }

    public async Task<TenantAppFileStorageSettings?> GetFileStorageSettingsAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var app = await _lowCodeAppQueryService.GetByIdAsync(tenantId, id, cancellationToken);
        if (app is null)
        {
            return null;
        }

        var appId = id.ToString();
        var options = _fileStorageOptionsMonitor.CurrentValue;

        var basePathConfig = await _systemConfigQueryService.GetByKeyAsync(
            tenantId,
            "FileStorage:BasePath",
            appId,
            cancellationToken);
        var bucketConfig = await _systemConfigQueryService.GetByKeyAsync(
            tenantId,
            "FileStorage:Minio:BucketName",
            appId,
            cancellationToken);

        var overrideBasePath = string.Equals(basePathConfig?.AppId, appId, StringComparison.OrdinalIgnoreCase)
            ? NormalizeValue(basePathConfig?.ConfigValue)
            : null;
        var overrideBucketName = string.Equals(bucketConfig?.AppId, appId, StringComparison.OrdinalIgnoreCase)
            ? NormalizeValue(bucketConfig?.ConfigValue)
            : null;

        var effectiveBasePath = NormalizeValue(basePathConfig?.ConfigValue) ?? options.BasePath;
        var effectiveBucketName = NormalizeValue(bucketConfig?.ConfigValue) ?? options.Minio.BucketName;

        return new TenantAppFileStorageSettings(
            id.ToString(),
            appId,
            effectiveBasePath,
            effectiveBucketName,
            overrideBasePath,
            overrideBucketName,
            overrideBasePath is null,
            overrideBucketName is null);
    }

    public async Task<IReadOnlyList<TenantAppDataSourceBindingDto>> GetDataSourceBindingsAsync(
        TenantId tenantId,
        IReadOnlyCollection<long>? appInstanceIds,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var appQuery = _db.Queryable<LowCodeApp>()
            .Where(app => app.TenantIdValue == tenantValue);
        if (appInstanceIds is { Count: > 0 })
        {
            var filterAppIds = appInstanceIds.ToArray();
            appQuery = appQuery.Where(app => SqlFunc.ContainsArray(filterAppIds, app.Id));
        }

        var apps = await appQuery
            .OrderByDescending(app => app.Id)
            .ToListAsync(cancellationToken);
        if (apps.Count == 0)
        {
            return [];
        }

        var appIds = apps.Select(app => app.Id).ToArray();
        var bindings = await _db.Queryable<TenantAppDataSourceBindingEntity>()
            .Where(binding => binding.TenantIdValue == tenantValue && SqlFunc.ContainsArray(appIds, binding.TenantAppInstanceId))
            .ToListAsync(cancellationToken);
        var preferredBindingsByAppId = bindings
            .GroupBy(binding => binding.TenantAppInstanceId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(binding => binding.IsActive)
                    .ThenBy(binding => binding.BindingType)
                    .ThenByDescending(binding => binding.UpdatedAt ?? binding.BoundAt)
                    .First());

        var legacyDataSourceIds = apps
            .Where(app => app.DataSourceId.HasValue && !preferredBindingsByAppId.ContainsKey(app.Id))
            .Select(app => app.DataSourceId!.Value);
        var dataSourceIds = preferredBindingsByAppId.Values
            .Select(binding => binding.DataSourceId)
            .Concat(legacyDataSourceIds)
            .Distinct()
            .ToArray();
        var tenantIdText = tenantValue.ToString();
        var dataSourceDict = new Dictionary<long, TenantDataSource>();
        if (dataSourceIds.Length > 0)
        {
            var tenantDataSources = await _db.Queryable<TenantDataSource>()
                .Where(ds => ds.TenantIdValue == tenantIdText && SqlFunc.ContainsArray(dataSourceIds, ds.Id))
                .ToListAsync(cancellationToken);
            dataSourceDict = tenantDataSources.ToDictionary(ds => ds.Id);
        }

        return apps
            .Select(app =>
            {
                if (preferredBindingsByAppId.TryGetValue(app.Id, out var binding))
                {
                    var dataSource = dataSourceDict.TryGetValue(binding.DataSourceId, out var bindingDataSource)
                        ? bindingDataSource
                        : null;
                    return new TenantAppDataSourceBindingDto(
                        app.Id.ToString(),
                        binding.DataSourceId.ToString(),
                        dataSource?.Name,
                        dataSource?.DbType,
                        dataSource?.IsActive,
                        dataSource?.LastTestedAt?.ToString("O"),
                        binding.Id.ToString(),
                        binding.BindingType.ToString(),
                        binding.IsActive,
                        binding.BoundAt.ToString("O"),
                        "BindingTable");
                }

                if (app.DataSourceId.HasValue)
                {
                    var dataSource = dataSourceDict.TryGetValue(app.DataSourceId.Value, out var legacyDataSource)
                        ? legacyDataSource
                        : null;
                    return new TenantAppDataSourceBindingDto(
                        app.Id.ToString(),
                        app.DataSourceId.Value.ToString(),
                        dataSource?.Name,
                        dataSource?.DbType,
                        dataSource?.IsActive,
                        dataSource?.LastTestedAt?.ToString("O"),
                        null,
                        TenantAppDataSourceBindingType.Primary.ToString(),
                        null,
                        null,
                        "LegacyLowCodeApp.DataSourceId");
                }

                return new TenantAppDataSourceBindingDto(
                    app.Id.ToString(),
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    "Unbound");
            })
            .ToArray();
    }

    private static string? NormalizeValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed class TenantAppInstanceCommandService : ITenantAppInstanceCommandService
{
    private readonly ILowCodeAppCommandService _commandService;
    private readonly ILowCodeAppQueryService _queryService;
    private readonly ISystemConfigCommandService _systemConfigCommandService;

    public TenantAppInstanceCommandService(
        ILowCodeAppCommandService commandService,
        ILowCodeAppQueryService queryService,
        ISystemConfigCommandService systemConfigCommandService)
    {
        _commandService = commandService;
        _queryService = queryService;
        _systemConfigCommandService = systemConfigCommandService;
    }

    public Task<long> CreateAsync(
        TenantId tenantId,
        long userId,
        LowCodeAppCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        return _commandService.CreateAsync(tenantId, userId, request, cancellationToken);
    }

    public Task UpdateAsync(
        TenantId tenantId,
        long userId,
        long id,
        LowCodeAppUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        return _commandService.UpdateAsync(tenantId, userId, id, request, cancellationToken);
    }

    public Task PublishAsync(
        TenantId tenantId,
        long userId,
        long id,
        CancellationToken cancellationToken = default)
    {
        return _commandService.PublishAsync(tenantId, userId, id, cancellationToken);
    }

    public Task UpdateEntityAliasesAsync(
        TenantId tenantId,
        long userId,
        long id,
        LowCodeAppEntityAliasesUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        return _commandService.UpdateEntityAliasesAsync(tenantId, userId, id, request, cancellationToken);
    }

    public async Task UpdateFileStorageSettingsAsync(
        TenantId tenantId,
        long userId,
        long id,
        TenantAppFileStorageSettingsUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var app = await _queryService.GetByIdAsync(tenantId, id, cancellationToken);
        if (app is null)
        {
            throw new InvalidOperationException("Tenant app instance not found.");
        }

        var appId = id.ToString();
        if (!request.InheritBasePath && string.IsNullOrWhiteSpace(request.OverrideBasePath))
        {
            throw new InvalidOperationException("OverrideBasePath is required when inheritance is disabled.");
        }

        if (!request.InheritMinioBucketName && string.IsNullOrWhiteSpace(request.OverrideMinioBucketName))
        {
            throw new InvalidOperationException("OverrideMinioBucketName is required when inheritance is disabled.");
        }

        var items = new List<SystemConfigBatchUpsertItem>();
        if (!request.InheritBasePath)
        {
            items.Add(new SystemConfigBatchUpsertItem(
                ConfigKey: "FileStorage:BasePath",
                ConfigValue: request.OverrideBasePath!.Trim(),
                ConfigName: "应用级本地存储根目录",
                Remark: $"app:{appId} override",
                ConfigType: "Text",
                TargetJson: null,
                AppId: appId,
                GroupName: "FileStorage",
                IsEncrypted: false,
                Version: null));
        }

        if (!request.InheritMinioBucketName)
        {
            items.Add(new SystemConfigBatchUpsertItem(
                ConfigKey: "FileStorage:Minio:BucketName",
                ConfigValue: request.OverrideMinioBucketName!.Trim(),
                ConfigName: "应用级 MinIO Bucket",
                Remark: $"app:{appId} override",
                ConfigType: "Text",
                TargetJson: null,
                AppId: appId,
                GroupName: "FileStorage",
                IsEncrypted: false,
                Version: null));
        }

        if (items.Count > 0)
        {
            await _systemConfigCommandService.BatchUpsertSystemConfigsAsync(
                tenantId,
                new SystemConfigBatchUpsertRequest(items),
                cancellationToken);
        }

        if (request.InheritBasePath)
        {
            await _systemConfigCommandService.DeleteSystemConfigByKeyAsync(
                tenantId,
                "FileStorage:BasePath",
                appId,
                cancellationToken);
        }

        if (request.InheritMinioBucketName)
        {
            await _systemConfigCommandService.DeleteSystemConfigByKeyAsync(
                tenantId,
                "FileStorage:Minio:BucketName",
                appId,
                cancellationToken);
        }
    }

    public Task DeleteAsync(
        TenantId tenantId,
        long userId,
        long id,
        CancellationToken cancellationToken = default)
    {
        return _commandService.DeleteAsync(tenantId, userId, id, cancellationToken);
    }

    public Task<LowCodeAppExportPackage?> ExportAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        return _queryService.ExportAsync(tenantId, id, cancellationToken);
    }

    public Task<LowCodeAppImportResult> ImportAsync(
        TenantId tenantId,
        long userId,
        LowCodeAppImportRequest request,
        CancellationToken cancellationToken = default)
    {
        return _commandService.ImportAsync(tenantId, userId, request, cancellationToken);
    }
}

public sealed class RuntimeContextQueryService : IRuntimeContextQueryService
{
    private static readonly HashSet<string> BlockingMigrationStatuses = new(StringComparer.Ordinal)
    {
        AppMigrationTaskStatuses.Pending,
        AppMigrationTaskStatuses.Prechecking,
        AppMigrationTaskStatuses.Running,
        AppMigrationTaskStatuses.Validating
    };

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

        var app = await _mainDb.Queryable<LowCodeApp>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppKey == appKey)
            .FirstAsync(cancellationToken);
        if (app is not null && app.Id > 0)
        {
            await EnsureRuntimeReadableAsync(tenantId, app.Id, cancellationToken);
            return await _appDbScopeFactory.GetAppClientAsync(tenantId, app.Id, cancellationToken);
        }

        return _mainDb;
    }

    private async Task EnsureRuntimeReadableAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken)
    {
        var latestTask = await _mainDb.Queryable<AppMigrationTask>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TenantAppInstanceId == appInstanceId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstAsync(cancellationToken);
        if (latestTask is null || !BlockingMigrationStatuses.Contains(latestTask.Status))
        {
            return;
        }

        throw new BusinessException(
            ErrorCodes.AppMigrationPending,
            "应用正在切库同步中，请稍后重试。");
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

        var appIds = await _mainDb.Queryable<LowCodeApp>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        foreach (var appId in appIds)
        {
            var appDb = await _appDbScopeFactory.GetAppClientAsync(tenantId, appId, cancellationToken);
            route = await appDb.Queryable<RuntimeRoute>()
                .FirstAsync(x => x.TenantIdValue == tenantId.Value && x.Id == id, cancellationToken);
            if (route is not null)
            {
                return route;
            }
        }

        return null;
    }
}

public sealed class RuntimeExecutionQueryService : IRuntimeExecutionQueryService
{
    private readonly ISqlSugarClient _mainDb;
    private readonly Atlas.Infrastructure.Services.IAppDbScopeFactory _appDbScopeFactory;

    public RuntimeExecutionQueryService(
        ISqlSugarClient db,
        Atlas.Infrastructure.Services.IAppDbScopeFactory appDbScopeFactory)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
    }

    public RuntimeExecutionQueryService(ISqlSugarClient db)
        : this(db, new Atlas.Infrastructure.Services.MainOnlyAppDbScopeFactory(db))
    {
    }

    public async Task<PagedResult<RuntimeExecutionListItem>> QueryAsync(
        TenantId tenantId,
        PagedRequest request,
        string? appId = null,
        string? status = null,
        DateTimeOffset? startedFrom = null,
        DateTimeOffset? startedTo = null,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;

        long? appIdValue = null;
        if (!string.IsNullOrWhiteSpace(appId))
        {
            if (!long.TryParse(appId, out var parsedAppId))
            {
                return new PagedResult<RuntimeExecutionListItem>(Array.Empty<RuntimeExecutionListItem>(), 0, pageIndex, pageSize);
            }
            appIdValue = parsedAppId;
        }

        ExecutionStatus? statusValue = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ExecutionStatus>(status, true, out var parsedStatus))
            {
                return new PagedResult<RuntimeExecutionListItem>(Array.Empty<RuntimeExecutionListItem>(), 0, pageIndex, pageSize);
            }
            statusValue = parsedStatus;
        }

        var db = await ResolveExecutionDbAsync(tenantId, appIdValue, cancellationToken);
        var query = db.Queryable<WorkflowExecution>()
            .Where(execution => execution.TenantIdValue == tenantValue);

        if (appIdValue.HasValue)
        {
            query = query.Where(execution => execution.AppId == appIdValue.Value);
        }

        if (statusValue.HasValue)
        {
            query = query.Where(execution => execution.Status == statusValue.Value);
        }

        if (startedFrom.HasValue)
        {
            query = query.Where(execution => execution.StartedAt >= startedFrom.Value.UtcDateTime);
        }

        if (startedTo.HasValue)
        {
            query = query.Where(execution => execution.StartedAt <= startedTo.Value.UtcDateTime);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            if (long.TryParse(keyword, out var idKeyword))
            {
                query = query.Where(execution =>
                    execution.WorkflowId == idKeyword
                    || execution.AppId == idKeyword
                    || execution.ReleaseId == idKeyword
                    || execution.RuntimeContextId == idKeyword
                    || (execution.ErrorMessage != null && execution.ErrorMessage.Contains(keyword)));
            }
            else if (Enum.TryParse<ExecutionStatus>(keyword, true, out var keywordStatus))
            {
                query = query.Where(execution =>
                    execution.Status == keywordStatus
                    || (execution.ErrorMessage != null && execution.ErrorMessage.Contains(keyword)));
            }
            else
            {
                query = query.Where(execution => execution.ErrorMessage != null && execution.ErrorMessage.Contains(keyword));
            }
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(execution => execution.StartedAt)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(execution => new RuntimeExecutionListItem(
            execution.Id.ToString(),
            execution.WorkflowId.ToString(),
            execution.RuntimeContextId?.ToString(),
            execution.ReleaseId?.ToString(),
            execution.AppId?.ToString(),
            execution.Status.ToString(),
            execution.StartedAt.ToString("O"),
            execution.CompletedAt?.ToString("O"),
            execution.ErrorMessage,
            ClassifyErrorCategory(execution.Status, execution.ErrorMessage))).ToArray();

        return new PagedResult<RuntimeExecutionListItem>(items, total, pageIndex, pageSize);
    }

    public async Task<RuntimeExecutionDetail?> GetByIdAsync(
        TenantId tenantId,
        long executionId,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var execution = await FindExecutionAcrossDbsAsync(tenantId, executionId, cancellationToken);
        if (execution is null)
        {
            return null;
        }

        return new RuntimeExecutionDetail(
            execution.Id.ToString(),
            execution.WorkflowId.ToString(),
            execution.RuntimeContextId?.ToString(),
            execution.ReleaseId?.ToString(),
            execution.AppId?.ToString(),
            execution.Status.ToString(),
            execution.StartedAt.ToString("O"),
            execution.CompletedAt?.ToString("O"),
            execution.InputsJson,
            execution.OutputsJson,
            execution.ErrorMessage);
    }

    public async Task<PagedResult<RuntimeExecutionAuditTrailItem>> GetAuditTrailsAsync(
        TenantId tenantId,
        long executionId,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var execution = await FindExecutionAcrossDbsAsync(tenantId, executionId, cancellationToken);
        var targetSet = BuildAuditTargetSet(executionId, execution);
        var auditTargets = targetSet.ToArray();
        var query = _mainDb.Queryable<AuditRecord>()
            .Where(item => item.TenantIdValue == tenantValue
                && SqlFunc.ContainsArray(auditTargets, item.Target));
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(item => item.Action.Contains(keyword) || item.Target.Contains(keyword) || item.Actor.Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(item => item.OccurredAt)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(item => new RuntimeExecutionAuditTrailItem(
            item.Id.ToString(),
            item.Actor,
            item.Action,
            item.Result,
            item.Target,
            item.OccurredAt.ToString("O")))
            .ToArray();

        return new PagedResult<RuntimeExecutionAuditTrailItem>(items, total, pageIndex, pageSize);
    }

    public async Task<RuntimeExecutionStats> GetStatsAsync(
        TenantId tenantId,
        string? appId = null,
        DateTimeOffset? startedFrom = null,
        DateTimeOffset? startedTo = null,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;

        long? appIdValue = null;
        if (!string.IsNullOrWhiteSpace(appId) && long.TryParse(appId, out var parsedAppId))
        {
            appIdValue = parsedAppId;
        }

        var db = await ResolveExecutionDbAsync(tenantId, appIdValue, cancellationToken);
        var query = db.Queryable<WorkflowExecution>()
            .Where(execution => execution.TenantIdValue == tenantValue);

        if (appIdValue.HasValue)
        {
            query = query.Where(execution => execution.AppId == appIdValue.Value);
        }

        if (startedFrom.HasValue)
        {
            query = query.Where(execution => execution.StartedAt >= startedFrom.Value.UtcDateTime);
        }

        if (startedTo.HasValue)
        {
            query = query.Where(execution => execution.StartedAt <= startedTo.Value.UtcDateTime);
        }

        var rows = await query
            .Select(execution => new
            {
                execution.Status,
                execution.StartedAt,
                execution.CompletedAt,
                execution.ErrorMessage
            })
            .ToListAsync(cancellationToken);

        var total = rows.Count;
        var running = rows.Count(r => r.Status == ExecutionStatus.Running || r.Status == ExecutionStatus.Pending);
        var succeeded = rows.Count(r => r.Status == ExecutionStatus.Completed);
        var failed = rows.Count(r => r.Status == ExecutionStatus.Failed);
        var cancelled = rows.Count(r => r.Status == ExecutionStatus.Cancelled);

        var durations = rows
            .Where(r => r.CompletedAt.HasValue)
            .Select(r => (r.CompletedAt!.Value - r.StartedAt).TotalMilliseconds)
            .OrderBy(d => d)
            .ToArray();

        double? avgDurationMs = durations.Length > 0 ? durations.Average() : null;
        double? p95DurationMs = null;
        if (durations.Length > 0)
        {
            var p95Index = (int)Math.Ceiling(durations.Length * 0.95) - 1;
            p95DurationMs = durations[Math.Max(0, p95Index)];
        }

        var errorCategories = rows
            .Where(r => r.Status == ExecutionStatus.Failed)
            .GroupBy(r => ClassifyErrorCategory(r.Status, r.ErrorMessage) ?? "Unknown")
            .ToDictionary(g => g.Key, g => (long)g.Count());

        return new RuntimeExecutionStats(
            total,
            running,
            succeeded,
            failed,
            cancelled,
            avgDurationMs,
            p95DurationMs,
            errorCategories);
    }

    private static string? ClassifyErrorCategory(ExecutionStatus status, string? errorMessage)
    {
        if (status != ExecutionStatus.Failed)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return "Unknown";
        }

        var msg = errorMessage.ToLowerInvariant();
        if (msg.Contains("timeout") || msg.Contains("超时") || msg.Contains("timed out"))
        {
            return "Timeout";
        }

        if (msg.Contains("network") || msg.Contains("connection") || msg.Contains("网络") || msg.Contains("连接"))
        {
            return "NetworkError";
        }

        if (msg.Contains("validation") || msg.Contains("invalid") || msg.Contains("校验") || msg.Contains("格式"))
        {
            return "ValidationError";
        }

        if (msg.Contains("config") || msg.Contains("配置") || msg.Contains("setting"))
        {
            return "ConfigError";
        }

        if (msg.Contains("permission") || msg.Contains("forbidden") || msg.Contains("unauthorized")
            || msg.Contains("权限") || msg.Contains("未授权"))
        {
            return "PermissionError";
        }

        return "Unknown";
    }

    private static HashSet<string> BuildAuditTargetSet(long executionId, WorkflowExecution? execution)
    {
        var executionIdText = executionId.ToString();
        var targetSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            executionIdText,
            $"WorkflowExecution:{executionIdText}",
            $"RuntimeExecution:{executionIdText}"
        };

        if (execution?.ReleaseId is { } releaseId)
        {
            targetSet.Add($"Release:{releaseId}");
            targetSet.Add($"AppRelease:{releaseId}");
        }

        if (execution?.RuntimeContextId is { } runtimeContextId)
        {
            targetSet.Add($"RuntimeContext:{runtimeContextId}");
            targetSet.Add($"RuntimeRoute:{runtimeContextId}");
        }

        if (execution?.AppId is { } appId)
        {
            targetSet.Add($"App:{appId}");
            targetSet.Add($"AppManifest:{appId}");
        }

        return targetSet;
    }

    private async Task<ISqlSugarClient> ResolveExecutionDbAsync(
        TenantId tenantId,
        long? appId,
        CancellationToken cancellationToken)
    {
        if (appId.HasValue && appId.Value > 0)
        {
            return await _appDbScopeFactory.GetAppClientAsync(tenantId, appId.Value, cancellationToken);
        }

        return _mainDb;
    }

    private async Task<WorkflowExecution?> FindExecutionAcrossDbsAsync(
        TenantId tenantId,
        long executionId,
        CancellationToken cancellationToken)
    {
        var execution = await _mainDb.Queryable<WorkflowExecution>()
            .FirstAsync(item => item.TenantIdValue == tenantId.Value && item.Id == executionId, cancellationToken);
        if (execution is not null)
        {
            return execution;
        }

        var appIds = await _mainDb.Queryable<LowCodeApp>()
            .Where(item => item.TenantIdValue == tenantId.Value)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        foreach (var appId in appIds)
        {
            var appDb = await _appDbScopeFactory.GetAppClientAsync(tenantId, appId, cancellationToken);
            execution = await appDb.Queryable<WorkflowExecution>()
                .FirstAsync(item => item.TenantIdValue == tenantId.Value && item.Id == executionId, cancellationToken);
            if (execution is not null)
            {
                return execution;
            }
        }

        return null;
    }
}

public sealed class RuntimeExecutionCommandService : IRuntimeExecutionCommandService
{
    private readonly ISqlSugarClient _mainDb;
    private readonly Atlas.Infrastructure.Services.IAppDbScopeFactory _appDbScopeFactory;
    private readonly IWorkflowV2ExecutionService _workflowExecutionService;

    public RuntimeExecutionCommandService(
        ISqlSugarClient db,
        Atlas.Infrastructure.Services.IAppDbScopeFactory appDbScopeFactory,
        IWorkflowV2ExecutionService workflowExecutionService)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
        _workflowExecutionService = workflowExecutionService;
    }

    public RuntimeExecutionCommandService(
        ISqlSugarClient db,
        IWorkflowV2ExecutionService workflowExecutionService)
        : this(db, new Atlas.Infrastructure.Services.MainOnlyAppDbScopeFactory(db), workflowExecutionService)
    {
    }

    public async Task<RuntimeExecutionOperationResult> CancelAsync(
        TenantId tenantId,
        long operatorUserId,
        long executionId,
        CancellationToken cancellationToken = default)
    {
        var execution = await GetExecutionAsync(tenantId, executionId, cancellationToken);
        if (execution.Status is ExecutionStatus.Completed or ExecutionStatus.Failed or ExecutionStatus.Cancelled)
        {
            return new RuntimeExecutionOperationResult(
                "cancel",
                executionId.ToString(),
                execution.Status.ToString(),
                "当前执行状态不支持取消。",
                null);
        }

        await _workflowExecutionService.CancelAsync(tenantId, executionId, cancellationToken);
        await WriteAuditAsync(tenantId, operatorUserId, "runtime.execution.cancel", $"RuntimeExecution:{executionId}", cancellationToken);
        return new RuntimeExecutionOperationResult("cancel", executionId.ToString(), ExecutionStatus.Cancelled.ToString(), "执行已取消。", null);
    }

    public async Task<RuntimeExecutionOperationResult> RetryAsync(
        TenantId tenantId,
        long operatorUserId,
        long executionId,
        CancellationToken cancellationToken = default)
    {
        var execution = await GetExecutionAsync(tenantId, executionId, cancellationToken);
        if (execution.Status is not (ExecutionStatus.Failed or ExecutionStatus.Cancelled or ExecutionStatus.Interrupted))
        {
            return new RuntimeExecutionOperationResult(
                "retry",
                executionId.ToString(),
                execution.Status.ToString(),
                "仅失败/取消/中断状态支持重试。",
                null);
        }

        var runResult = await _workflowExecutionService.AsyncRunAsync(
            tenantId,
            execution.WorkflowId,
            operatorUserId,
            new WorkflowV2RunRequest(execution.InputsJson),
            cancellationToken);
        await WriteAuditAsync(
            tenantId,
            operatorUserId,
            "runtime.execution.retry",
            $"RuntimeExecution:{executionId}->{runResult.ExecutionId}",
            cancellationToken);

        return new RuntimeExecutionOperationResult(
            "retry",
            executionId.ToString(),
            runResult.Status?.ToString() ?? ExecutionStatus.Pending.ToString(),
            "已发起重试执行。",
            runResult.ExecutionId);
    }

    public async Task<RuntimeExecutionOperationResult> ResumeAsync(
        TenantId tenantId,
        long operatorUserId,
        long executionId,
        CancellationToken cancellationToken = default)
    {
        var execution = await GetExecutionAsync(tenantId, executionId, cancellationToken);
        if (execution.Status != ExecutionStatus.Interrupted)
        {
            return new RuntimeExecutionOperationResult(
                "resume",
                executionId.ToString(),
                execution.Status.ToString(),
                "仅中断状态支持恢复。",
                null);
        }

        await _workflowExecutionService.ResumeAsync(tenantId, executionId, cancellationToken);
        await WriteAuditAsync(tenantId, operatorUserId, "runtime.execution.resume", $"RuntimeExecution:{executionId}", cancellationToken);
        return new RuntimeExecutionOperationResult("resume", executionId.ToString(), ExecutionStatus.Running.ToString(), "执行已恢复。", null);
    }

    public async Task<RuntimeExecutionOperationResult> DebugAsync(
        TenantId tenantId,
        long operatorUserId,
        long executionId,
        RuntimeExecutionDebugRequest request,
        CancellationToken cancellationToken = default)
    {
        var execution = await GetExecutionAsync(tenantId, executionId, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.NodeKey))
        {
            throw new InvalidOperationException("NodeKey 不能为空。");
        }

        var debugRequest = new WorkflowV2NodeDebugRequest(request.NodeKey.Trim(), request.InputsJson ?? execution.InputsJson);
        var debugResult = await _workflowExecutionService.DebugNodeAsync(
            tenantId,
            execution.WorkflowId,
            operatorUserId,
            debugRequest,
            cancellationToken);
        await WriteAuditAsync(
            tenantId,
            operatorUserId,
            "runtime.execution.debug",
            $"RuntimeExecution:{executionId}->{debugResult.ExecutionId}",
            cancellationToken);

        return new RuntimeExecutionOperationResult(
            "debug",
            executionId.ToString(),
            debugResult.Status?.ToString() ?? ExecutionStatus.Pending.ToString(),
            "单节点调试执行已完成。",
            debugResult.ExecutionId);
    }

    public async Task<RuntimeExecutionTimeoutDiagnosis?> GetTimeoutDiagnosisAsync(
        TenantId tenantId,
        long executionId,
        CancellationToken cancellationToken = default)
    {
        var execution = await FindExecutionAcrossDbsAsync(tenantId, executionId, cancellationToken);
        if (execution is null)
        {
            return null;
        }

        var completedAt = execution.CompletedAt;
        var elapsed = (completedAt ?? DateTime.UtcNow) - execution.StartedAt;
        var elapsedSeconds = Math.Max(0, elapsed.TotalSeconds);
        var timeoutRisk = execution.Status == ExecutionStatus.Running && elapsedSeconds >= 300;
        var diagnosis = timeoutRisk
            ? "执行运行时间超过 5 分钟，存在超时风险。"
            : execution.Status switch
            {
                ExecutionStatus.Failed => $"执行失败：{execution.ErrorMessage ?? "未记录异常信息"}",
                ExecutionStatus.Interrupted => $"执行中断：{execution.InterruptType}",
                ExecutionStatus.Cancelled => "执行已取消。",
                _ => "执行状态正常。"
            };

        var suggestions = new List<string>();
        if (timeoutRisk)
        {
            suggestions.Add("检查外部依赖（数据库/API）响应时间。");
            suggestions.Add("考虑对执行进行 cancel 后 retry，或拆分长耗时节点。");
        }

        if (execution.Status == ExecutionStatus.Failed)
        {
            suggestions.Add("查看 ErrorMessage 并定位失败节点。");
            suggestions.Add("修复后执行 retry。");
        }

        if (execution.Status == ExecutionStatus.Interrupted)
        {
            suggestions.Add("确认中断原因并执行 resume。");
        }

        if (suggestions.Count == 0)
        {
            suggestions.Add("当前无需额外处理。");
        }

        return new RuntimeExecutionTimeoutDiagnosis(
            execution.Id.ToString(),
            execution.Status.ToString(),
            execution.StartedAt.ToString("O"),
            completedAt?.ToString("O"),
            elapsedSeconds,
            timeoutRisk,
            diagnosis,
            suggestions);
    }

    private async Task<WorkflowExecution> GetExecutionAsync(
        TenantId tenantId,
        long executionId,
        CancellationToken cancellationToken)
    {
        return await FindExecutionAcrossDbsAsync(tenantId, executionId, cancellationToken)
            ?? throw new InvalidOperationException("执行实例不存在。");
    }

    private async Task WriteAuditAsync(
        TenantId tenantId,
        long operatorUserId,
        string action,
        string target,
        CancellationToken cancellationToken)
    {
        var audit = new AuditRecord(
            tenantId,
            operatorUserId.ToString(),
            action,
            "Success",
            target,
            null,
            null);
        await _mainDb.Insertable(audit).ExecuteCommandAsync(cancellationToken);
    }

    private async Task<WorkflowExecution?> FindExecutionAcrossDbsAsync(
        TenantId tenantId,
        long executionId,
        CancellationToken cancellationToken)
    {
        var execution = await _mainDb.Queryable<WorkflowExecution>()
            .FirstAsync(item => item.TenantIdValue == tenantId.Value && item.Id == executionId, cancellationToken);
        if (execution is not null)
        {
            return execution;
        }

        var appIds = await _mainDb.Queryable<LowCodeApp>()
            .Where(item => item.TenantIdValue == tenantId.Value)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        foreach (var appId in appIds)
        {
            var appDb = await _appDbScopeFactory.GetAppClientAsync(tenantId, appId, cancellationToken);
            execution = await appDb.Queryable<WorkflowExecution>()
                .FirstAsync(item => item.TenantIdValue == tenantId.Value && item.Id == executionId, cancellationToken);
            if (execution is not null)
            {
                return execution;
            }
        }

        return null;
    }
}

public sealed class ResourceCenterQueryService : IResourceCenterQueryService
{
    private readonly ISqlSugarClient _mainDb;
    private readonly Atlas.Infrastructure.Services.IAppDbScopeFactory _appDbScopeFactory;
    private readonly ILogger<ResourceCenterQueryService> _logger;

    public ResourceCenterQueryService(
        ISqlSugarClient db,
        Atlas.Infrastructure.Services.IAppDbScopeFactory appDbScopeFactory,
        ILogger<ResourceCenterQueryService> logger)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
        _logger = logger;
    }

    public ResourceCenterQueryService(ISqlSugarClient db)
        : this(
            db,
            new Atlas.Infrastructure.Services.MainOnlyAppDbScopeFactory(db),
            NullLogger<ResourceCenterQueryService>.Instance)
    {
    }

    public async Task<ResourceCenterGroupsResponse> GetGroupsAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenantIdValue = tenantId.Value;
        var tenantIdText = tenantId.ToString();

        var catalogsTask = _mainDb.Queryable<AppManifest>()
            .Where(item => item.TenantIdValue == tenantIdValue)
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        var tenantApplicationsTask = _mainDb.Queryable<TenantApplication>()
            .Where(item => item.TenantIdValue == tenantIdValue)
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        var appInstancesTask = _mainDb.Queryable<LowCodeApp>()
            .Where(item => item.TenantIdValue == tenantIdValue)
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        var dataSourcesTask = _mainDb.Queryable<TenantDataSource>()
            .Where(item => item.TenantIdValue == tenantIdText)
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        var releasesTask = _mainDb.Queryable<AppRelease>()
            .Where(item => item.TenantIdValue == tenantIdValue)
            .OrderByDescending(item => item.ReleasedAt)
            .ToListAsync(cancellationToken);
        var auditsTask = _mainDb.Queryable<AuditRecord>()
            .Where(item => item.TenantIdValue == tenantIdValue)
            .OrderByDescending(item => item.OccurredAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        await Task.WhenAll(
            catalogsTask,
            tenantApplicationsTask,
            appInstancesTask,
            dataSourcesTask,
            releasesTask,
            auditsTask);

        var catalogs = catalogsTask.Result;
        var tenantApplications = tenantApplicationsTask.Result;
        var appInstances = appInstancesTask.Result;
        var dataSources = dataSourcesTask.Result;
        var releases = releasesTask.Result;
        var runtimeContextResult = await LoadRuntimeRoutesAcrossAppsAsync(tenantId, appInstances, cancellationToken);
        var runtimeExecutionResult = await LoadRuntimeExecutionsAcrossAppsAsync(tenantId, appInstances, cancellationToken);
        var runtimeContexts = runtimeContextResult.Items;
        var runtimeExecutions = runtimeExecutionResult.Items;
        var warnings = runtimeContextResult.Warnings
            .Concat(runtimeExecutionResult.Warnings)
            .GroupBy(item => item.AppInstanceId)
            .Select(group => group.First())
            .ToArray();
        var audits = auditsTask.Result;

        var catalogById = catalogs.ToDictionary(item => item.Id);
        var appInstanceById = appInstances.ToDictionary(item => item.Id);

        var catalogItems = catalogs
            .Select(item => new ResourceCenterGroupEntry(
                item.Id.ToString(),
                item.Name,
                "ApplicationCatalog",
                item.Status.ToString(),
                item.Description,
                $"/console/application-catalogs?catalogId={item.Id}",
                item.Id.ToString()))
            .ToArray();
        var tenantApplicationItems = tenantApplications
            .Select(item => new ResourceCenterGroupEntry(
                item.Id.ToString(),
                item.Name,
                "TenantApplication",
                item.Status.ToString(),
                $"AppKey={item.AppKey}",
                $"/console/tenant-applications?applicationId={item.Id}",
                item.CatalogId.ToString(),
                item.AppInstanceId.ToString()))
            .ToArray();
        var appInstanceItems = appInstances
            .Select(item => new ResourceCenterGroupEntry(
                item.Id.ToString(),
                item.Name,
                "TenantAppInstance",
                item.Status.ToString(),
                item.Description,
                $"/console/tenant-app-instances?instanceId={item.Id}",
                null,
                item.Id.ToString()))
            .ToArray();
        var dataSourceItems = dataSources
            .Select(item => new ResourceCenterGroupEntry(
                item.Id.ToString(),
                item.Name,
                "TenantDataSource",
                item.IsActive ? "Active" : "Disabled",
                item.LastTestMessage,
                $"/console/datasource-consumption?dataSourceId={item.Id}",
                null,
                item.AppId?.ToString()))
            .ToArray();
        var releaseItems = releases
            .Select(item =>
            {
                var manifest = catalogById.TryGetValue(item.ManifestId, out var manifestValue) ? manifestValue : null;
                return new ResourceCenterGroupEntry(
                    item.Id.ToString(),
                    $"v{item.Version}",
                    "Release",
                    item.Status.ToString(),
                    manifest is null ? item.ReleaseNote : $"{manifest.Name} / {manifest.AppKey}",
                    $"/console/releases?releaseId={item.Id}",
                    item.ManifestId.ToString(),
                    null,
                    item.Id.ToString());
            })
            .ToArray();
        var runtimeContextItems = runtimeContexts
            .Select(item => new ResourceCenterGroupEntry(
                item.Id.ToString(),
                $"{item.AppKey}/{item.PageKey}",
                "RuntimeContext",
                item.IsActive ? "Active" : "Inactive",
                $"SchemaVersion={item.SchemaVersion}, Env={item.EnvironmentCode}",
                $"/console/runtime-contexts?contextId={item.Id}",
                item.ManifestId.ToString(),
                null,
                null,
                item.Id.ToString()))
            .ToArray();
        var runtimeExecutionItems = runtimeExecutions
            .Select(item => new ResourceCenterGroupEntry(
                item.Id.ToString(),
                $"Workflow:{item.WorkflowId}",
                "RuntimeExecution",
                item.Status.ToString(),
                $"StartedAt={item.StartedAt:O}",
                $"/console/runtime-executions?executionId={item.Id}",
                null,
                item.AppId?.ToString(),
                item.ReleaseId?.ToString(),
                item.RuntimeContextId?.ToString(),
                item.Id.ToString()))
            .ToArray();
        var auditSummaryItems = audits
            .GroupBy(item => new { item.Action, item.Result })
            .OrderByDescending(group => group.Count())
            .Take(20)
            .Select(group => new ResourceCenterGroupEntry(
                $"{group.Key.Action}:{group.Key.Result}",
                group.Key.Action,
                "AuditSummary",
                group.Key.Result,
                $"近200条记录命中 {group.Count()} 次",
                $"/audit?keyword={Uri.EscapeDataString(group.Key.Action)}"))
            .ToArray();
        var debugEntryItems = audits
            .Where(item =>
                item.Action.Contains("debug", StringComparison.OrdinalIgnoreCase) ||
                item.Target.Contains("debug", StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .Select(item => new ResourceCenterGroupEntry(
                $"{item.Action}:{item.OccurredAt:yyyyMMddHHmmssfff}",
                item.Action,
                "DebugEntry",
                item.Result,
                $"{item.Target} @ {item.OccurredAt:O}",
                "/console/debug"))
            .ToArray();

        var groups = new[]
        {
            new ResourceCenterGroupItem("catalogs", "应用目录", catalogItems.Length, catalogItems),
            new ResourceCenterGroupItem("tenant-applications", "租户开通关系", tenantApplicationItems.Length, tenantApplicationItems),
            new ResourceCenterGroupItem("instances", "租户应用实例", appInstanceItems.Length, appInstanceItems),
            new ResourceCenterGroupItem("releases", "发布记录", releaseItems.Length, releaseItems),
            new ResourceCenterGroupItem("runtime-contexts", "运行上下文", runtimeContextItems.Length, runtimeContextItems),
            new ResourceCenterGroupItem("runtime-executions", "运行执行", runtimeExecutionItems.Length, runtimeExecutionItems),
            new ResourceCenterGroupItem("datasources", "租户数据源", dataSourceItems.Length, dataSourceItems),
            new ResourceCenterGroupItem("audit-summary", "审计汇总", auditSummaryItems.Length, auditSummaryItems),
            new ResourceCenterGroupItem("debug-entries", "调试记录", debugEntryItems.Length, debugEntryItems)
        };
        var warningItems = warnings
            .Select(item => new ResourceCenterWarningItem(
                item.AppInstanceId.ToString(),
                item.AppName,
                item.ErrorCode,
                item.Message))
            .ToArray();
        return new ResourceCenterGroupsResponse(groups, warningItems);
    }

    public async Task<ResourceCenterDataSourceConsumptionResponse> GetDataSourceConsumptionAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenantIdValue = tenantId.Value;
        var tenantIdText = tenantIdValue.ToString();

        var dataSourcesTask = _mainDb.Queryable<TenantDataSource>()
            .Where(item => item.TenantIdValue == tenantIdText)
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        var appInstancesTask = _mainDb.Queryable<LowCodeApp>()
            .Where(item => item.TenantIdValue == tenantIdValue)
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        var bindingsTask = _mainDb.Queryable<TenantAppDataSourceBindingEntity>()
            .Where(item => item.TenantIdValue == tenantIdValue)
            .OrderByDescending(item => item.UpdatedAt ?? item.BoundAt)
            .ToListAsync(cancellationToken);
        await Task.WhenAll(dataSourcesTask, appInstancesTask, bindingsTask);

        var dataSources = dataSourcesTask.Result;
        var appInstances = appInstancesTask.Result;
        var bindings = bindingsTask.Result;

        var appInstanceById = appInstances.ToDictionary(item => item.Id);
        var bindingAppSet = bindings
            .Select(item => item.TenantAppInstanceId)
            .ToHashSet();
        var allRelations = bindings
            .Select(item => new DataSourceBindingRelation(
                item.Id.ToString(),
                item.TenantAppInstanceId,
                item.DataSourceId,
                item.BindingType.ToString(),
                item.IsActive,
                item.BoundAt,
                item.UpdatedAt,
                "BindingTable"))
            .ToList();
        allRelations.AddRange(
            appInstances
                .Where(item => item.DataSourceId.HasValue && !bindingAppSet.Contains(item.Id))
                .Select(item => new DataSourceBindingRelation(
                    $"legacy-{item.Id}-{item.DataSourceId!.Value}",
                    item.Id,
                    item.DataSourceId!.Value,
                    TenantAppDataSourceBindingType.Primary.ToString(),
                    true,
                    null,
                    null,
                    "LegacyLowCodeApp.DataSourceId")));

        var activeBoundAppSet = allRelations
            .Where(item => item.IsActive)
            .Select(item => item.TenantAppInstanceId)
            .ToHashSet();
        var relationsByDataSourceId = allRelations
            .GroupBy(item => item.DataSourceId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.IsActive)
                    .ThenBy(item => item.BindingType)
                    .ThenByDescending(item => item.UpdatedAt ?? item.BoundAt ?? DateTimeOffset.MinValue)
                    .ToArray());
        var duplicateCountByKey = dataSources
            .GroupBy(item =>
            {
                var scope = item.AppId.HasValue ? "app" : "platform";
                return $"{scope}:{item.AppId?.ToString() ?? "0"}:{item.DbType}:{item.Name}".ToLowerInvariant();
            })
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        TenantDataSourceConsumptionItem MapDataSource(TenantDataSource dataSource)
        {
            var relations = relationsByDataSourceId.TryGetValue(dataSource.Id, out var dataSourceRelations)
                ? dataSourceRelations
                : [];
            var bindingRelations = relations
                .Select(item => new TenantDataSourceBindingRelationItem(
                    item.BindingId,
                    item.TenantAppInstanceId.ToString(),
                    item.DataSourceId.ToString(),
                    item.BindingType,
                    item.IsActive,
                    item.BoundAt?.ToString("O"),
                    item.UpdatedAt?.ToString("O"),
                    item.Source))
                .ToArray();
            var boundApps = relations
                .Where(item => item.IsActive)
                .Select(item => appInstanceById.TryGetValue(item.TenantAppInstanceId, out var app) ? MapAppConsumer(app) : null)
                .Where(item => item is not null)
                .GroupBy(item => item!.TenantAppInstanceId, StringComparer.Ordinal)
                .Select(group => group.First()!)
                .ToArray();

            var scope = dataSource.AppId.HasValue ? "AppScoped" : "Platform";
            string? scopeAppId = null;
            string? scopeAppName = null;
            if (dataSource.AppId.HasValue && appInstanceById.TryGetValue(dataSource.AppId.Value, out var scopeApp))
            {
                scopeAppId = scopeApp.Id.ToString();
                scopeAppName = scopeApp.Name;
            }
            else if (dataSource.AppId.HasValue)
            {
                scopeAppId = dataSource.AppId.Value.ToString();
            }

            var duplicateScopeKey = dataSource.AppId.HasValue ? "app" : "platform";
            var duplicateKey = $"{duplicateScopeKey}:{dataSource.AppId?.ToString() ?? "0"}:{dataSource.DbType}:{dataSource.Name}".ToLowerInvariant();
            var isDuplicate = duplicateCountByKey.TryGetValue(duplicateKey, out var duplicateCount) && duplicateCount > 1;
            var isOrphan = dataSource.AppId.HasValue && !appInstanceById.ContainsKey(dataSource.AppId.Value);
            var isUnbound = boundApps.Length == 0;
            var testMessage = dataSource.LastTestMessage ?? string.Empty;
            var isInvalid = !dataSource.IsActive
                || testMessage.Contains("失败", StringComparison.OrdinalIgnoreCase)
                || testMessage.Contains("error", StringComparison.OrdinalIgnoreCase)
                || testMessage.Contains("exception", StringComparison.OrdinalIgnoreCase);
            var impactScope = scope == "Platform"
                ? $"Platform / BoundApps:{boundApps.Length}"
                : $"AppScoped:{scopeAppName ?? scopeAppId ?? "-"} / BoundApps:{boundApps.Length}";
            var repairSuggestion = isOrphan
                ? "建议执行“解绑孤儿绑定”"
                : isInvalid
                    ? "建议执行“禁用无效绑定”"
                    : isDuplicate
                        ? "建议执行“切换主绑定”并清理重复绑定"
                        : isUnbound
                            ? "建议执行“切换主绑定”补齐有效绑定"
                            : "无需修复";

            return new TenantDataSourceConsumptionItem(
                dataSource.Id.ToString(),
                dataSource.Name,
                dataSource.DbType,
                dataSource.IsActive,
                scope,
                scopeAppId,
                scopeAppName,
                boundApps.Length,
                boundApps,
                bindingRelations,
                dataSource.LastTestedAt?.ToString("O"),
                dataSource.LastTestMessage,
                isOrphan,
                isDuplicate,
                isInvalid,
                isUnbound,
                impactScope,
                repairSuggestion);
        }

        var platformDataSources = dataSources
            .Where(item => !item.AppId.HasValue)
            .Select(MapDataSource)
            .ToArray();
        var appScopedDataSources = dataSources
            .Where(item => item.AppId.HasValue)
            .Select(MapDataSource)
            .ToArray();
        var unboundTenantApps = appInstances
            .Where(item => !activeBoundAppSet.Contains(item.Id))
            .Select(MapAppConsumer)
            .ToArray();

        return new ResourceCenterDataSourceConsumptionResponse(
            platformDataSources.Length,
            appScopedDataSources.Length,
            unboundTenantApps.Length,
            platformDataSources,
            appScopedDataSources,
            unboundTenantApps);
    }

    private static TenantAppConsumerItem MapAppConsumer(LowCodeApp app)
    {
        return new TenantAppConsumerItem(
            app.Id.ToString(),
            app.AppKey,
            app.Name,
            app.Status.ToString());
    }

    private sealed record DataSourceBindingRelation(
        string BindingId,
        long TenantAppInstanceId,
        long DataSourceId,
        string BindingType,
        bool IsActive,
        DateTimeOffset? BoundAt,
        DateTimeOffset? UpdatedAt,
        string Source);

    private async Task<ResourceCenterAppLoadResult<RuntimeRoute>> LoadRuntimeRoutesAcrossAppsAsync(
        TenantId tenantId,
        IReadOnlyList<LowCodeApp> appInstances,
        CancellationToken cancellationToken)
    {
        var tasks = new List<Task<ResourceCenterPerAppLoadResult<RuntimeRoute>>>
        {
            LoadMainDbRoutesAsync(tenantId, cancellationToken)
        };
        tasks.AddRange(appInstances.Select(async app =>
        {
            try
            {
                var appDb = await _appDbScopeFactory.GetAppClientAsync(tenantId, app.Id, cancellationToken);
                var items = await appDb.Queryable<RuntimeRoute>()
                    .Where(item => item.TenantIdValue == tenantId.Value)
                    .ToListAsync(cancellationToken);
                return ResourceCenterPerAppLoadResult<RuntimeRoute>.Success(items);
            }
            catch (BusinessException ex) when (ShouldSkipNoDataSource(ex))
            {
                _logger.LogWarning(
                    "资源中心路由聚合跳过未绑定数据源应用。TenantId={TenantId}; AppInstanceId={AppInstanceId}; AppName={AppName}; ErrorCode={ErrorCode}",
                    tenantId.Value,
                    app.Id,
                    app.Name,
                    ex.Code);
                return ResourceCenterPerAppLoadResult<RuntimeRoute>.Skipped(
                    new ResourceCenterAppLoadWarning(
                        app.Id,
                        app.Name,
                        ex.Code,
                        ex.Message));
            }
        }));

        await Task.WhenAll(tasks);
        var results = tasks.Select(task => task.Result).ToArray();
        var items = results
            .SelectMany(task => task.Items)
            .GroupBy(item => item.Id)
            .Select(group => group.First())
            .OrderByDescending(item => item.Id)
            .ToArray();
        var warnings = results
            .Select(item => item.Warning)
            .Where(item => item is not null)
            .Select(item => item!)
            .ToArray();
        return new ResourceCenterAppLoadResult<RuntimeRoute>(items, warnings);
    }

    private async Task<ResourceCenterAppLoadResult<WorkflowExecution>> LoadRuntimeExecutionsAcrossAppsAsync(
        TenantId tenantId,
        IReadOnlyList<LowCodeApp> appInstances,
        CancellationToken cancellationToken)
    {
        var tasks = new List<Task<ResourceCenterPerAppLoadResult<WorkflowExecution>>>
        {
            LoadMainDbExecutionsAsync(tenantId, cancellationToken)
        };
        tasks.AddRange(appInstances.Select(async app =>
        {
            try
            {
                var appDb = await _appDbScopeFactory.GetAppClientAsync(tenantId, app.Id, cancellationToken);
                var items = await appDb.Queryable<WorkflowExecution>()
                    .Where(item => item.TenantIdValue == tenantId.Value)
                    .Take(200)
                    .ToListAsync(cancellationToken);
                return ResourceCenterPerAppLoadResult<WorkflowExecution>.Success(items);
            }
            catch (BusinessException ex) when (ShouldSkipNoDataSource(ex))
            {
                _logger.LogWarning(
                    "资源中心执行聚合跳过未绑定数据源应用。TenantId={TenantId}; AppInstanceId={AppInstanceId}; AppName={AppName}; ErrorCode={ErrorCode}",
                    tenantId.Value,
                    app.Id,
                    app.Name,
                    ex.Code);
                return ResourceCenterPerAppLoadResult<WorkflowExecution>.Skipped(
                    new ResourceCenterAppLoadWarning(
                        app.Id,
                        app.Name,
                        ex.Code,
                        ex.Message));
            }
        }));

        await Task.WhenAll(tasks);
        var results = tasks.Select(task => task.Result).ToArray();
        var items = results
            .SelectMany(task => task.Items)
            .GroupBy(item => item.Id)
            .Select(group => group.First())
            .OrderByDescending(item => item.StartedAt)
            .Take(200)
            .ToArray();
        var warnings = results
            .Select(item => item.Warning)
            .Where(item => item is not null)
            .Select(item => item!)
            .ToArray();
        return new ResourceCenterAppLoadResult<WorkflowExecution>(items, warnings);
    }

    private async Task<ResourceCenterPerAppLoadResult<RuntimeRoute>> LoadMainDbRoutesAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var items = await _mainDb.Queryable<RuntimeRoute>()
            .Where(item => item.TenantIdValue == tenantId.Value)
            .ToListAsync(cancellationToken);
        return ResourceCenterPerAppLoadResult<RuntimeRoute>.Success(items);
    }

    private async Task<ResourceCenterPerAppLoadResult<WorkflowExecution>> LoadMainDbExecutionsAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var items = await _mainDb.Queryable<WorkflowExecution>()
            .Where(item => item.TenantIdValue == tenantId.Value)
            .Take(200)
            .ToListAsync(cancellationToken);
        return ResourceCenterPerAppLoadResult<WorkflowExecution>.Success(items);
    }

    private static bool ShouldSkipNoDataSource(BusinessException ex)
    {
        return string.Equals(ex.Code, ErrorCodes.ValidationError, StringComparison.Ordinal)
            && ex.Message.Contains("未绑定可用数据源", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ResourceCenterPerAppLoadResult<T>(
        IReadOnlyList<T> Items,
        ResourceCenterAppLoadWarning? Warning)
    {
        public static ResourceCenterPerAppLoadResult<T> Success(IReadOnlyList<T> items)
            => new(items, null);

        public static ResourceCenterPerAppLoadResult<T> Skipped(ResourceCenterAppLoadWarning warning)
            => new(Array.Empty<T>(), warning);
    }

    private sealed record ResourceCenterAppLoadResult<T>(
        IReadOnlyList<T> Items,
        IReadOnlyList<ResourceCenterAppLoadWarning> Warnings);

    private sealed record ResourceCenterAppLoadWarning(
        long AppInstanceId,
        string AppName,
        string ErrorCode,
        string Message);
}

public sealed class ResourceCenterCommandService : IResourceCenterCommandService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly ITenantDbConnectionFactory _tenantDbConnectionFactory;

    public ResourceCenterCommandService(
        ISqlSugarClient db,
        IIdGeneratorAccessor idGenerator,
        ITenantDbConnectionFactory tenantDbConnectionFactory)
    {
        _db = db;
        _idGenerator = idGenerator;
        _tenantDbConnectionFactory = tenantDbConnectionFactory;
    }

    public async Task<ResourceCenterRepairResult> DisableInvalidBindingAsync(
        TenantId tenantId,
        long operatorUserId,
        DisableInvalidBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(request.BindingId, out var bindingId) || bindingId <= 0)
        {
            throw new InvalidOperationException("BindingId 无效。");
        }

        var tenantValue = tenantId.Value;
        var binding = await _db.Queryable<TenantAppDataSourceBindingEntity>()
            .FirstAsync(item => item.TenantIdValue == tenantValue && item.Id == bindingId, cancellationToken)
            ?? throw new InvalidOperationException("绑定关系不存在。");
        if (!binding.IsActive)
        {
            return new ResourceCenterRepairResult("disable-invalid-binding", request.BindingId, true, "绑定已处于禁用状态。");
        }

        var now = DateTimeOffset.UtcNow;
        binding.Deactivate(operatorUserId, now, "resource-center:disable-invalid-binding");
        var audit = new AuditRecord(
            tenantId,
            operatorUserId.ToString(),
            "resource.datasource-binding.disable-invalid",
            "Success",
            $"Binding:{binding.Id}",
            null,
            null);

        var transaction = await _db.Ado.UseTranAsync(async () =>
        {
            await _db.Updateable(binding).ExecuteCommandAsync(cancellationToken);
            await _db.Insertable(audit).ExecuteCommandAsync(cancellationToken);
        });

        if (!transaction.IsSuccess)
        {
            throw transaction.ErrorException ?? new InvalidOperationException("禁用无效绑定失败。");
        }

        _tenantDbConnectionFactory.InvalidateCache(tenantId.Value.ToString("D"), binding.TenantAppInstanceId);

        return new ResourceCenterRepairResult("disable-invalid-binding", binding.Id.ToString(), true, "已禁用无效绑定。");
    }

    public async Task<ResourceCenterRepairResult> SwitchPrimaryBindingAsync(
        TenantId tenantId,
        long operatorUserId,
        SwitchPrimaryBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(request.TenantAppInstanceId, out var appInstanceId) || appInstanceId <= 0)
        {
            throw new InvalidOperationException("TenantAppInstanceId 无效。");
        }

        if (!long.TryParse(request.TargetDataSourceId, out var targetDataSourceId) || targetDataSourceId <= 0)
        {
            throw new InvalidOperationException("TargetDataSourceId 无效。");
        }

        var tenantValue = tenantId.Value;
        var tenantText = tenantValue.ToString();

        var app = await _db.Queryable<LowCodeApp>()
            .FirstAsync(item => item.TenantIdValue == tenantValue && item.Id == appInstanceId, cancellationToken)
            ?? throw new InvalidOperationException("应用实例不存在。");
        var targetDataSource = await _db.Queryable<TenantDataSource>()
            .FirstAsync(item => item.TenantIdValue == tenantText && item.Id == targetDataSourceId, cancellationToken)
            ?? throw new InvalidOperationException("目标数据源不存在。");
        var bindings = await _db.Queryable<TenantAppDataSourceBindingEntity>()
            .Where(item => item.TenantIdValue == tenantValue && item.TenantAppInstanceId == appInstanceId)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var updates = new List<TenantAppDataSourceBindingEntity>();
        foreach (var binding in bindings.Where(item =>
                     item.IsActive
                     && item.BindingType == TenantAppDataSourceBindingType.Primary
                     && item.DataSourceId != targetDataSourceId))
        {
            binding.Deactivate(operatorUserId, now, "resource-center:switch-primary-binding");
            updates.Add(binding);
        }

        var targetBinding = bindings.FirstOrDefault(item => item.DataSourceId == targetDataSourceId);
        TenantAppDataSourceBindingEntity? newTargetBinding = null;
        if (targetBinding is null)
        {
            newTargetBinding = new TenantAppDataSourceBindingEntity(
                tenantId,
                appInstanceId,
                targetDataSourceId,
                TenantAppDataSourceBindingType.Primary,
                operatorUserId,
                _idGenerator.NextId(),
                now,
                request.Note);
            targetBinding = newTargetBinding;
        }
        else
        {
            targetBinding.Rebind(
                targetDataSourceId,
                TenantAppDataSourceBindingType.Primary,
                operatorUserId,
                now,
                request.Note);
            updates.Add(targetBinding);
        }

        app.Update(app.Name, app.Description, app.Category, app.Icon, targetDataSource.Id, operatorUserId, now);
        var audit = new AuditRecord(
            tenantId,
            operatorUserId.ToString(),
            "resource.datasource-binding.switch-primary",
            "Success",
            $"App:{app.Id}/DataSource:{targetDataSource.Id}",
            null,
            null);

        var transaction = await _db.Ado.UseTranAsync(async () =>
        {
            if (updates.Count > 0)
            {
                await _db.Updateable(updates).ExecuteCommandAsync(cancellationToken);
            }

            if (newTargetBinding is not null)
            {
                await _db.Insertable(newTargetBinding).ExecuteCommandAsync(cancellationToken);
            }

            await _db.Updateable(app).ExecuteCommandAsync(cancellationToken);
            await _db.Insertable(audit).ExecuteCommandAsync(cancellationToken);
        });

        if (!transaction.IsSuccess)
        {
            throw transaction.ErrorException ?? new InvalidOperationException("切换主绑定失败。");
        }

        _tenantDbConnectionFactory.InvalidateCache(tenantId.Value.ToString("D"), appInstanceId);

        return new ResourceCenterRepairResult(
            "switch-primary-binding",
            targetBinding.Id.ToString(),
            true,
            $"应用 {app.Name} 已切换到数据源 {targetDataSource.Name}。");
    }

    public async Task<ResourceCenterRepairResult> UnbindOrphanBindingAsync(
        TenantId tenantId,
        long operatorUserId,
        UnbindOrphanBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(request.BindingId, out var bindingId) || bindingId <= 0)
        {
            throw new InvalidOperationException("BindingId 无效。");
        }

        var tenantValue = tenantId.Value;
        var tenantText = tenantValue.ToString();
        var binding = await _db.Queryable<TenantAppDataSourceBindingEntity>()
            .FirstAsync(item => item.TenantIdValue == tenantValue && item.Id == bindingId, cancellationToken)
            ?? throw new InvalidOperationException("绑定关系不存在。");
        var app = await _db.Queryable<LowCodeApp>()
            .FirstAsync(item => item.TenantIdValue == tenantValue && item.Id == binding.TenantAppInstanceId, cancellationToken);
        var dataSourceExists = await _db.Queryable<TenantDataSource>()
            .AnyAsync(item => item.TenantIdValue == tenantText && item.Id == binding.DataSourceId, cancellationToken);
        var isOrphan = app is null || !dataSourceExists;
        if (!isOrphan)
        {
            return new ResourceCenterRepairResult("unbind-orphan-binding", request.BindingId, true, "绑定不是孤儿关系，无需解绑。");
        }

        var now = DateTimeOffset.UtcNow;
        binding.Deactivate(operatorUserId, now, "resource-center:unbind-orphan-binding");
        if (app is not null && app.DataSourceId == binding.DataSourceId)
        {
            app.Update(app.Name, app.Description, app.Category, app.Icon, null, operatorUserId, now);
        }

        var audit = new AuditRecord(
            tenantId,
            operatorUserId.ToString(),
            "resource.datasource-binding.unbind-orphan",
            "Success",
            $"Binding:{binding.Id}",
            null,
            null);

        var transaction = await _db.Ado.UseTranAsync(async () =>
        {
            await _db.Updateable(binding).ExecuteCommandAsync(cancellationToken);
            if (app is not null)
            {
                await _db.Updateable(app).ExecuteCommandAsync(cancellationToken);
            }

            await _db.Insertable(audit).ExecuteCommandAsync(cancellationToken);
        });

        if (!transaction.IsSuccess)
        {
            throw transaction.ErrorException ?? new InvalidOperationException("解绑孤儿绑定失败。");
        }

        _tenantDbConnectionFactory.InvalidateCache(tenantId.Value.ToString("D"), binding.TenantAppInstanceId);

        return new ResourceCenterRepairResult("unbind-orphan-binding", binding.Id.ToString(), true, "孤儿绑定已解绑。");
    }
}

public sealed class ReleaseCenterQueryService : IReleaseCenterQueryService
{
    private readonly ISqlSugarClient _mainDb;
    private readonly Atlas.Infrastructure.Services.IAppDbScopeFactory _appDbScopeFactory;

    public ReleaseCenterQueryService(
        ISqlSugarClient db,
        Atlas.Infrastructure.Services.IAppDbScopeFactory appDbScopeFactory)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
    }

    public ReleaseCenterQueryService(ISqlSugarClient db)
        : this(db, new Atlas.Infrastructure.Services.MainOnlyAppDbScopeFactory(db))
    {
    }

    public async Task<PagedResult<ReleaseCenterListItem>> QueryAsync(
        TenantId tenantId,
        PagedRequest request,
        string? status = null,
        string? appKey = null,
        long? manifestId = null,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;

        var manifestList = await _mainDb.Queryable<AppManifest>()
            .Where(item => item.TenantIdValue == tenantValue)
            .Select(item => new { item.Id, item.Name, item.AppKey })
            .ToListAsync(cancellationToken);
        var manifestDict = manifestList.ToDictionary(item => item.Id);

        var query = _mainDb.Queryable<AppRelease>()
            .Where(item => item.TenantIdValue == tenantValue);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(item => item.ReleaseNote.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AppReleaseStatus>(status, true, out var statusValue))
        {
            query = query.Where(item => item.Status == statusValue);
        }

        if (manifestId.HasValue && manifestId.Value > 0)
        {
            query = query.Where(item => item.ManifestId == manifestId.Value);
        }

        if (!string.IsNullOrWhiteSpace(appKey))
        {
            var appKeyValue = appKey.Trim();
            var manifestIds = manifestList
                .Where(item => item.AppKey.Contains(appKeyValue, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Id)
                .ToArray();
            if (manifestIds.Length == 0)
            {
                return new PagedResult<ReleaseCenterListItem>(Array.Empty<ReleaseCenterListItem>(), 0, pageIndex, pageSize);
            }

            query = query.Where(item => SqlFunc.ContainsArray(manifestIds, item.ManifestId));
        }

        var total = await query.CountAsync(cancellationToken);
        var releases = await query
            .OrderByDescending(item => item.ReleasedAt)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = releases
            .Select(item =>
            {
                var manifest = manifestDict.TryGetValue(item.ManifestId, out var manifestValue)
                    ? manifestValue
                    : null;
                return new ReleaseCenterListItem(
                    item.Id.ToString(),
                    item.ManifestId.ToString(),
                    manifest?.Name ?? "Unknown",
                    manifest?.AppKey ?? string.Empty,
                    item.Version,
                    item.Status.ToString(),
                    item.ReleasedAt.ToString("O"),
                    item.ReleaseNote);
            })
            .ToArray();

        return new PagedResult<ReleaseCenterListItem>(items, total, pageIndex, pageSize);
    }

    public async Task<ReleaseCenterDetail?> GetByIdAsync(
        TenantId tenantId,
        long releaseId,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var release = await _mainDb.Queryable<AppRelease>()
            .FirstAsync(item => item.TenantIdValue == tenantValue && item.Id == releaseId, cancellationToken);
        if (release is null)
        {
            return null;
        }

        var manifest = await _mainDb.Queryable<AppManifest>()
            .FirstAsync(item => item.TenantIdValue == tenantValue && item.Id == release.ManifestId, cancellationToken);
        return new ReleaseCenterDetail(
            release.Id.ToString(),
            release.ManifestId.ToString(),
            manifest?.Name ?? "Unknown",
            manifest?.AppKey ?? string.Empty,
            release.Version,
            release.Status.ToString(),
            release.ReleasedAt.ToString("O"),
            release.ReleaseNote,
            release.SnapshotJson);
    }

    public async Task<ReleaseDiffSummary?> GetDiffAsync(
        TenantId tenantId,
        long releaseId,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var release = await _mainDb.Queryable<AppRelease>()
            .FirstAsync(item => item.TenantIdValue == tenantValue && item.Id == releaseId, cancellationToken);
        if (release is null)
        {
            return null;
        }

        var baselineRelease = await _mainDb.Queryable<AppRelease>()
            .Where(item => item.TenantIdValue == tenantValue && item.ManifestId == release.ManifestId && item.Id != releaseId)
            .OrderByDescending(item => item.ReleasedAt)
            .FirstAsync(cancellationToken);

        var currentSnapshot = FlattenSnapshot(release.SnapshotJson);
        var baselineSnapshot = baselineRelease is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : FlattenSnapshot(baselineRelease.SnapshotJson);

        var addedKeys = currentSnapshot.Keys
            .Where(key => !baselineSnapshot.ContainsKey(key))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var removedKeys = baselineSnapshot.Keys
            .Where(key => !currentSnapshot.ContainsKey(key))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var changedKeys = currentSnapshot.Keys
            .Where(key =>
                baselineSnapshot.TryGetValue(key, out var baselineValue) &&
                !string.Equals(currentSnapshot[key], baselineValue, StringComparison.Ordinal))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ReleaseDiffSummary(
            release.Id.ToString(),
            baselineRelease?.Id.ToString(),
            addedKeys.Length,
            removedKeys.Length,
            changedKeys.Length,
            addedKeys,
            removedKeys,
            changedKeys);
    }

    public async Task<ReleaseImpactSummary?> GetImpactAsync(
        TenantId tenantId,
        long releaseId,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var release = await _mainDb.Queryable<AppRelease>()
            .FirstAsync(item => item.TenantIdValue == tenantValue && item.Id == releaseId, cancellationToken);
        if (release is null)
        {
            return null;
        }

        var manifest = await _mainDb.Queryable<AppManifest>()
            .FirstAsync(item => item.TenantIdValue == tenantValue && item.Id == release.ManifestId, cancellationToken);
        if (manifest is null)
        {
            return null;
        }

        var app = await _mainDb.Queryable<LowCodeApp>()
            .FirstAsync(item => item.TenantIdValue == tenantValue && item.AppKey == manifest.AppKey, cancellationToken);
        var runtimeDb = app is not null
            ? await _appDbScopeFactory.GetAppClientAsync(tenantId, app.Id, cancellationToken)
            : _mainDb;
        var routeQuery = runtimeDb.Queryable<RuntimeRoute>()
            .Where(item => item.TenantIdValue == tenantValue && item.ManifestId == release.ManifestId);
        var executionQuery = runtimeDb.Queryable<WorkflowExecution>()
            .Where(item => item.TenantIdValue == tenantValue && item.ReleaseId == release.Id);
        var since = DateTime.UtcNow.AddHours(-24);

        var runtimeRouteCountTask = routeQuery.CountAsync(cancellationToken);
        var activeRuntimeRouteCountTask = routeQuery.CountAsync(item => item.IsActive, cancellationToken);
        var recentExecutionCountTask = executionQuery.CountAsync(item => item.StartedAt >= since, cancellationToken);
        var runningExecutionCountTask = executionQuery.CountAsync(item => item.Status == ExecutionStatus.Running, cancellationToken);
        var failedExecutionCountTask = executionQuery.CountAsync(item => item.Status == ExecutionStatus.Failed, cancellationToken);

        await Task.WhenAll(
            runtimeRouteCountTask,
            activeRuntimeRouteCountTask,
            recentExecutionCountTask,
            runningExecutionCountTask,
            failedExecutionCountTask);

        var runtimeRouteCount = runtimeRouteCountTask.Result;
        return new ReleaseImpactSummary(
            release.Id.ToString(),
            manifest.AppKey,
            runtimeRouteCount,
            activeRuntimeRouteCountTask.Result,
            runtimeRouteCount,
            recentExecutionCountTask.Result,
            runningExecutionCountTask.Result,
            failedExecutionCountTask.Result);
    }

    private static Dictionary<string, string> FlattenSnapshot(string snapshotJson)
    {
        if (string.IsNullOrWhiteSpace(snapshotJson))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var document = JsonDocument.Parse(snapshotJson);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            PopulateSnapshotFields(document.RootElement, "$", result);
            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["$"] = snapshotJson
            };
        }
    }

    private static void PopulateSnapshotFields(
        JsonElement element,
        string path,
        IDictionary<string, string> output)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                foreach (var property in element.EnumerateObject())
                {
                    var childPath = $"{path}.{property.Name}";
                    PopulateSnapshotFields(property.Value, childPath, output);
                }

                break;
            }
            case JsonValueKind.Array:
            {
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var childPath = $"{path}[{index}]";
                    PopulateSnapshotFields(item, childPath, output);
                    index++;
                }

                break;
            }
            case JsonValueKind.String:
                output[path] = element.GetString() ?? string.Empty;
                break;
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                output[path] = element.ToString();
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                output[path] = string.Empty;
                break;
            default:
                output[path] = element.ToString();
                break;
        }
    }
}

public sealed class CozeMappingQueryService : ICozeMappingQueryService
{
    private readonly ISqlSugarClient _mainDb;
    private readonly Atlas.Infrastructure.Services.IAppDbScopeFactory _appDbScopeFactory;

    public CozeMappingQueryService(
        ISqlSugarClient db,
        Atlas.Infrastructure.Services.IAppDbScopeFactory appDbScopeFactory)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
    }

    public CozeMappingQueryService(ISqlSugarClient db)
        : this(db, new Atlas.Infrastructure.Services.MainOnlyAppDbScopeFactory(db))
    {
    }

    public async Task<CozeLayerMappingOverview> GetOverviewAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var catalogsCountTask = _mainDb.Queryable<AppManifest>()
            .Where(item => item.TenantIdValue == tenantValue)
            .CountAsync(cancellationToken);
        var appInstancesCountTask = _mainDb.Queryable<LowCodeApp>()
            .Where(item => item.TenantIdValue == tenantValue)
            .CountAsync(cancellationToken);
        var releasesCountTask = _mainDb.Queryable<AppRelease>()
            .Where(item => item.TenantIdValue == tenantValue)
            .CountAsync(cancellationToken);
        var appInstancesTask = _mainDb.Queryable<LowCodeApp>()
            .Where(item => item.TenantIdValue == tenantValue)
            .ToListAsync(cancellationToken);
        var auditCountTask = _mainDb.Queryable<AuditRecord>()
            .Where(item => item.TenantIdValue == tenantValue)
            .CountAsync(cancellationToken);
        await Task.WhenAll(catalogsCountTask, appInstancesCountTask, releasesCountTask, appInstancesTask, auditCountTask);
        var runtimeContextCount = await CountRuntimeRoutesAcrossAppsAsync(tenantId, appInstancesTask.Result, cancellationToken);
        var runtimeExecutionCount = await CountRuntimeExecutionsAcrossAppsAsync(tenantId, appInstancesTask.Result, cancellationToken);

        var layers = new[]
        {
            new CozeLayerMappingItem("L1", "应用目录层(ApplicationCatalog)", catalogsCountTask.Result, "平台侧目录定义"),
            new CozeLayerMappingItem("L2", "租户实例层(TenantAppInstance)", appInstancesCountTask.Result, "租户开通后的实例"),
            new CozeLayerMappingItem("L3", "发布层(ReleaseCenter)", releasesCountTask.Result, "发布版本与回滚点"),
            new CozeLayerMappingItem("L4", "运行上下文层(RuntimeContext)", runtimeContextCount, "运行路由与页面上下文"),
            new CozeLayerMappingItem("L5", "执行层(RuntimeExecution)", runtimeExecutionCount, "运行执行记录与状态"),
            new CozeLayerMappingItem("L6", "审计层(AuditTrail)", auditCountTask.Result, "操作与执行追溯证据")
        };

        return new CozeLayerMappingOverview(layers);
    }

    private async Task<int> CountRuntimeRoutesAcrossAppsAsync(
        TenantId tenantId,
        IReadOnlyList<LowCodeApp> appInstances,
        CancellationToken cancellationToken)
    {
        var tasks = new List<Task<List<long>>>
        {
            _mainDb.Queryable<RuntimeRoute>()
                .Where(item => item.TenantIdValue == tenantId.Value)
                .Select(item => item.Id)
                .ToListAsync(cancellationToken)
        };
        tasks.AddRange(appInstances.Select(async app =>
        {
            var appDb = await _appDbScopeFactory.GetAppClientAsync(tenantId, app.Id, cancellationToken);
            return await appDb.Queryable<RuntimeRoute>()
                .Where(item => item.TenantIdValue == tenantId.Value)
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);
        }));

        await Task.WhenAll(tasks);
        return tasks
            .SelectMany(task => task.Result)
            .Distinct()
            .Count();
    }

    private async Task<int> CountRuntimeExecutionsAcrossAppsAsync(
        TenantId tenantId,
        IReadOnlyList<LowCodeApp> appInstances,
        CancellationToken cancellationToken)
    {
        var tasks = new List<Task<List<long>>>
        {
            _mainDb.Queryable<WorkflowExecution>()
                .Where(item => item.TenantIdValue == tenantId.Value)
                .Select(item => item.Id)
                .ToListAsync(cancellationToken)
        };
        tasks.AddRange(appInstances.Select(async app =>
        {
            var appDb = await _appDbScopeFactory.GetAppClientAsync(tenantId, app.Id, cancellationToken);
            return await appDb.Queryable<WorkflowExecution>()
                .Where(item => item.TenantIdValue == tenantId.Value)
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);
        }));

        await Task.WhenAll(tasks);
        return tasks
            .SelectMany(task => task.Result)
            .Distinct()
            .Count();
    }
}

public sealed class DebugLayerQueryService : IDebugLayerQueryService
{
    private readonly ISqlSugarClient _db;

    public DebugLayerQueryService(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<DebugLayerEmbedMetadata> GetEmbedMetadataAsync(
        TenantId tenantId,
        long userId,
        string appId,
        long? projectId,
        bool projectScopeEnabled,
        CancellationToken cancellationToken = default)
    {
        var roleIds = await _db.Queryable<UserRole>()
            .Where(item => item.TenantIdValue == tenantId.Value && item.UserId == userId)
            .Select(item => item.RoleId)
            .ToListAsync(cancellationToken);
        var grantedPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (roleIds.Count > 0)
        {
            var roleIdArray = roleIds.Distinct().ToArray();
            var permissionIds = await _db.Queryable<RolePermission>()
                .Where(item => item.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(roleIdArray, item.RoleId))
                .Select(item => item.PermissionId)
                .Distinct()
                .ToListAsync(cancellationToken);
            if (permissionIds.Count > 0)
            {
                var permissionIdArray = permissionIds.ToArray();
                var permissionCodes = await _db.Queryable<Permission>()
                    .Where(item => item.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(permissionIdArray, item.Id))
                    .Select(item => item.Code)
                    .ToListAsync(cancellationToken);
                grantedPermissions = permissionCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
        }

        var resourceDefinitions = new[]
        {
            new DebugLayerResourceItem("workflow-executions", "运行执行观测", PermissionCodes.DebugView, "查看执行状态、输入输出与错误消息"),
            new DebugLayerResourceItem("runtime-audit-trails", "运行审计追溯", PermissionCodes.DebugRun, "查看与执行ID关联的审计轨迹"),
            new DebugLayerResourceItem("rollback-ops", "回滚操作入口", PermissionCodes.DebugManage, "触发发布版本回滚操作")
        };
        var resources = resourceDefinitions
            .Where(item => grantedPermissions.Contains(item.RequiredPermission))
            .ToArray();

        return new DebugLayerEmbedMetadata(
            tenantId.ToString(),
            appId,
            projectId?.ToString(),
            projectScopeEnabled,
            resources);
    }
}
