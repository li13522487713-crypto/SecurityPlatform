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

