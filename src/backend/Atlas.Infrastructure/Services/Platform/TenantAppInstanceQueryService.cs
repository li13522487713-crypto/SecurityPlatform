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


public sealed class TenantAppInstanceQueryService : ITenantAppInstanceQueryService
{
    private readonly ILowCodeAppQueryService _lowCodeAppQueryService;
    private readonly ITenantDataSourceService _tenantDataSourceService;
    private readonly ISystemConfigQueryService _systemConfigQueryService;
    private readonly IOptionsMonitor<FileStorageOptions> _fileStorageOptionsMonitor;
    private readonly IAppRuntimeSupervisor _appRuntimeSupervisor;
    private readonly ISqlSugarClient _db;

    public TenantAppInstanceQueryService(
        ILowCodeAppQueryService lowCodeAppQueryService,
        ITenantDataSourceService tenantDataSourceService,
        ISystemConfigQueryService systemConfigQueryService,
        IOptionsMonitor<FileStorageOptions> fileStorageOptionsMonitor,
        IAppRuntimeSupervisor appRuntimeSupervisor,
        ISqlSugarClient db)
    {
        _lowCodeAppQueryService = lowCodeAppQueryService;
        _tenantDataSourceService = tenantDataSourceService;
        _systemConfigQueryService = systemConfigQueryService;
        _fileStorageOptionsMonitor = fileStorageOptionsMonitor;
        _appRuntimeSupervisor = appRuntimeSupervisor;
        _db = db;
    }

    public async Task<PagedResult<TenantAppInstanceListItem>> QueryAsync(
        TenantId tenantId,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _lowCodeAppQueryService.QueryAsync(request, tenantId, null, cancellationToken);
        var appInstanceIds = result.Items
            .Select(item => long.TryParse(item.Id, out var parsedId) ? parsedId : 0L)
            .Where(idValue => idValue > 0)
            .ToArray();
        var runtimeSnapshots = appInstanceIds.Length == 0
            ? new Dictionary<long, TenantAppInstanceRuntimeInfo>()
            : new Dictionary<long, TenantAppInstanceRuntimeInfo>(
                await _appRuntimeSupervisor.GetRuntimeSnapshotMapAsync(tenantId, appInstanceIds, cancellationToken));
        var items = result.Items
            .Select(item =>
            {
                runtimeSnapshots.TryGetValue(long.Parse(item.Id), out var runtimeInfo);
                return new TenantAppInstanceListItem(
                    item.Id,
                    item.AppKey,
                    item.Name,
                    item.Status,
                    item.Version,
                    item.Description,
                    item.Category,
                    item.Icon,
                    item.PublishedAt?.ToString("O"),
                    runtimeInfo?.CurrentArtifactId,
                    runtimeInfo?.RuntimeStatus,
                    runtimeInfo?.HealthStatus,
                    runtimeInfo?.AssignedPort,
                    runtimeInfo?.CurrentPid,
                    runtimeInfo?.IngressUrl);
            })
            .ToArray();

        return new PagedResult<TenantAppInstanceListItem>(items, result.Total, result.PageIndex, result.PageSize);
    }

    public async Task<TenantAppInstanceDetail?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var item = await _db.Queryable<LowCodeApp>()
            .Where(app => app.TenantIdValue == tenantValue && app.Id == id)
            .FirstAsync(cancellationToken);
        if (item is null)
        {
            return null;
        }

        var pageCount = await _db.Queryable<LowCodePage>()
            .Where(page => page.TenantIdValue == tenantValue && page.AppId == id)
            .CountAsync(cancellationToken);
        var runtimeInfo = await _appRuntimeSupervisor.GetRuntimeInfoAsync(tenantId, id, cancellationToken);

        return new TenantAppInstanceDetail(
            item.Id.ToString(),
            item.AppKey,
            item.Name,
            item.Status.ToString(),
            item.Version,
            item.Description,
            item.Category,
            item.Icon,
            item.PublishedAt?.ToString("O"),
            item.DataSourceId?.ToString(),
            pageCount,
            runtimeInfo?.CurrentArtifactId,
            runtimeInfo?.RuntimeStatus,
            runtimeInfo?.HealthStatus,
            runtimeInfo?.AssignedPort,
            runtimeInfo?.CurrentPid,
            runtimeInfo?.IngressUrl,
            runtimeInfo?.LoginUrl,
            runtimeInfo?.InstanceHome,
            runtimeInfo?.StartedAt,
            runtimeInfo?.LastHealthCheckedAt);
    }

    public Task<TenantAppInstanceRuntimeInfo?> GetRuntimeInfoAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        return _appRuntimeSupervisor.GetRuntimeInfoAsync(tenantId, id, cancellationToken);
    }

    public Task<TenantAppInstanceHealthInfo?> GetHealthAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        return _appRuntimeSupervisor.GetHealthAsync(tenantId, id, cancellationToken);
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

        var basePathTask = _systemConfigQueryService.GetByKeyAsync(
            tenantId,
            "FileStorage:BasePath",
            appId,
            cancellationToken);
        var bucketTask = _systemConfigQueryService.GetByKeyAsync(
            tenantId,
            "FileStorage:Minio:BucketName",
            appId,
            cancellationToken);
        await Task.WhenAll(basePathTask, bucketTask);
        var basePathConfig = await basePathTask;
        var bucketConfig = await bucketTask;

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

