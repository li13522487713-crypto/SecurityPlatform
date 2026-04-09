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


public sealed class ReleaseCenterQueryService : IReleaseCenterQueryService
{
    private readonly ISqlSugarClient _mainDb;
    private readonly Atlas.Infrastructure.Services.IAppDbScopeFactory _appDbScopeFactory;
    private readonly IAppReleaseOrchestrator _appReleaseOrchestrator;

    public ReleaseCenterQueryService(
        ISqlSugarClient db,
        Atlas.Infrastructure.Services.IAppDbScopeFactory appDbScopeFactory,
        IAppReleaseOrchestrator appReleaseOrchestrator)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
        _appReleaseOrchestrator = appReleaseOrchestrator;
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

    public Task<ReleaseInstallStatusInfo?> GetInstallStatusAsync(
        TenantId tenantId,
        long releaseId,
        long tenantAppInstanceId,
        CancellationToken cancellationToken = default)
    {
        return _appReleaseOrchestrator.GetInstallStatusAsync(tenantId, releaseId, tenantAppInstanceId, cancellationToken);
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

