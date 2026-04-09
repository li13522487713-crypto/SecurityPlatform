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


public sealed class TenantAppInstanceCommandService : ITenantAppInstanceCommandService
{
    private readonly ILowCodeAppCommandService _commandService;
    private readonly ILowCodeAppQueryService _queryService;
    private readonly ISystemConfigCommandService _systemConfigCommandService;
    private readonly IAppRuntimeSupervisor _appRuntimeSupervisor;
    private readonly ISqlSugarClient _db;

    public TenantAppInstanceCommandService(
        ILowCodeAppCommandService commandService,
        ILowCodeAppQueryService queryService,
        ISystemConfigCommandService systemConfigCommandService,
        IAppRuntimeSupervisor appRuntimeSupervisor,
        ISqlSugarClient db)
    {
        _commandService = commandService;
        _queryService = queryService;
        _systemConfigCommandService = systemConfigCommandService;
        _appRuntimeSupervisor = appRuntimeSupervisor;
        _db = db;
    }

    public async Task<TenantAppInstanceRuntimeInfo> StartAsync(
        TenantId tenantId,
        long userId,
        long id,
        CancellationToken cancellationToken = default)
    {
        _ = userId;
        var registration = await BuildRuntimeRegistrationAsync(tenantId, id, cancellationToken);
        return await _appRuntimeSupervisor.StartAsync(tenantId, registration, cancellationToken);
    }

    public async Task<TenantAppInstanceRuntimeInfo> StopAsync(
        TenantId tenantId,
        long userId,
        long id,
        CancellationToken cancellationToken = default)
    {
        _ = userId;
        var registration = await BuildRuntimeRegistrationAsync(tenantId, id, cancellationToken);
        return await _appRuntimeSupervisor.StopAsync(tenantId, registration, cancellationToken);
    }

    public async Task<TenantAppInstanceRuntimeInfo> RestartAsync(
        TenantId tenantId,
        long userId,
        long id,
        CancellationToken cancellationToken = default)
    {
        _ = userId;
        var registration = await BuildRuntimeRegistrationAsync(tenantId, id, cancellationToken);
        return await _appRuntimeSupervisor.RestartAsync(tenantId, registration, cancellationToken);
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

    private async Task<TenantAppInstanceRuntimeRegistration> BuildRuntimeRegistrationAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        var app = await _db.Queryable<LowCodeApp>()
            .Where(row => row.TenantIdValue == tenantId.Value && row.Id == id)
            .FirstAsync(cancellationToken);
        if (app is null)
        {
            throw new InvalidOperationException("Tenant app instance not found.");
        }

        return new TenantAppInstanceRuntimeRegistration(
            app.Id.ToString(),
            app.AppKey,
            app.Name,
            app.Version,
            $"release-v{app.Version}");
    }
}

