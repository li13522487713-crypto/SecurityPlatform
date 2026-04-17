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


public sealed class CozeMappingQueryService : ICozeMappingQueryService
{
    private const int CozePerAppConcurrency = 4;
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
        var appInstancesCountTask = _mainDb.Queryable<TenantApplication>()
            .Where(item => item.TenantIdValue == tenantValue)
            .CountAsync(cancellationToken);
        var releasesCountTask = _mainDb.Queryable<AppRelease>()
            .Where(item => item.TenantIdValue == tenantValue)
            .CountAsync(cancellationToken);
        var appInstancesTask = _mainDb.Queryable<TenantApplication>()
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
        IReadOnlyList<TenantApplication> appInstances,
        CancellationToken cancellationToken)
    {
        using var gate = new SemaphoreSlim(CozePerAppConcurrency);
        var tasks = new List<Task<List<long>>>
        {
            _mainDb.Queryable<RuntimeRoute>()
                .Where(item => item.TenantIdValue == tenantId.Value)
                .Select(item => item.Id)
                .ToListAsync(cancellationToken)
        };
        tasks.AddRange(appInstances.Select(async app =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var appDb = await _appDbScopeFactory.TryGetAppClientAsync(tenantId, app.AppInstanceId, cancellationToken);
                if (appDb is null)
                {
                    return [];
                }
                return await appDb.Queryable<RuntimeRoute>()
                    .Where(item => item.TenantIdValue == tenantId.Value)
                    .Select(item => item.Id)
                    .ToListAsync(cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }));

        await Task.WhenAll(tasks);
        return tasks
            .SelectMany(task => task.Result)
            .Distinct()
            .Count();
    }

    private async Task<int> CountRuntimeExecutionsAcrossAppsAsync(
        TenantId tenantId,
        IReadOnlyList<TenantApplication> appInstances,
        CancellationToken cancellationToken)
    {
        using var gate = new SemaphoreSlim(CozePerAppConcurrency);
        var tasks = new List<Task<List<long>>>
        {
            _mainDb.Queryable<WorkflowExecution>()
                .Where(item => item.TenantIdValue == tenantId.Value)
                .Select(item => item.Id)
                .ToListAsync(cancellationToken)
        };
        tasks.AddRange(appInstances.Select(async app =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var appDb = await _appDbScopeFactory.TryGetAppClientAsync(tenantId, app.AppInstanceId, cancellationToken);
                if (appDb is null)
                {
                    return [];
                }
                return await appDb.Queryable<WorkflowExecution>()
                    .Where(item => item.TenantIdValue == tenantId.Value)
                    .Select(item => item.Id)
                    .ToListAsync(cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }));

        await Task.WhenAll(tasks);
        return tasks
            .SelectMany(task => task.Result)
            .Distinct()
            .Count();
    }

}

