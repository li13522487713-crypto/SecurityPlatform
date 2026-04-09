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

